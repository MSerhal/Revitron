# generate_rag_prompt.py
# -*- coding: utf-8 -*-

import os
import sys
import json
import glob
import difflib
import traceback
from datetime import datetime
from pathlib import Path
import logging

import chromadb
from chromadb.utils import embedding_functions
from sentence_transformers import SentenceTransformer
import torch
import anthropic  # Claude API

from config import (
    ANTHROPIC_API_KEY,
    MODEL_NAME,
    EMBEDDER_MODEL,
)

# Setup logging
log_path = os.path.join(os.path.dirname(__file__), "rag_debug.log")
for handler in logging.root.handlers[:]:
    logging.root.removeHandler(handler)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s: %(message)s",
    handlers=[
        logging.FileHandler(log_path, mode='a', encoding='utf-8'),
        logging.StreamHandler(sys.stderr)
    ]
)

# Configuration
base_dir = os.path.abspath(os.path.dirname(__file__))
persist_directory = base_dir
collection_name = "revit_api_collection"
model_name = EMBEDDER_MODEL
transformer_device = 'cuda' if torch.cuda.is_available() else 'cpu'
success_dir = os.path.join(base_dir, "scripts")
MAX_TOKENS = 1024

logging.info(f"Embedder model in use: {model_name}")
logging.info(f"LLM in use: {MODEL_NAME}")

# ChromaDB client init
def init_chroma():
    client = chromadb.PersistentClient(path=persist_directory)
    embedding_function = embedding_functions.SentenceTransformerEmbeddingFunction(
        model_name=model_name,
        device=transformer_device,
        trust_remote_code=True
    )
    return client.get_collection(name=collection_name, embedding_function=embedding_function)

# Try to reuse similar successful script
def try_reuse_script(user_query):
    if not os.path.isdir(success_dir):
        logging.warning("No success_dir found, skipping reuse.")
        return None

    script_files = glob.glob(os.path.join(success_dir, "*.py"))
    best_match = None
    highest_ratio = 0.0

    for script in script_files:
        filename = os.path.basename(script)
        ratio = difflib.SequenceMatcher(None, user_query.lower(), filename.lower()).ratio()
        if ratio > highest_ratio:
            highest_ratio = ratio
            best_match = script

    if highest_ratio > 0.75 and best_match:
        try:
            with open(best_match, "r", encoding="utf-8") as f:
                logging.info(f"Reused script matched with ratio {highest_ratio:.2f}: {best_match}")
                return f.read()
        except Exception as e:
            logging.error(f"Failed to read reused script: {e}")
    return None

# Generate context using Chroma
def fallback_rag_prompt(user_query):
    try:
        collection = init_chroma()
        results = collection.query(
            query_texts=[user_query],
            n_results=10,
            include=["documents"]
        )
        docs = [doc for group in results["documents"] for doc in group if doc.strip()]
        context = "\n\n---\n\n".join(docs[:15]) or "# No relevant context retrieved."
    except Exception as e:
        logging.error(f"Chroma query failed: {e}")
        context = "# No relevant documentation snippets found."

    prompt = f"""ROLE: You are a Revit API assistant.

TASK: Generate Python code for IronPython/RevitPythonShell.

RESPONSE FORMAT: Only valid Python code — no markdown fences, comments, or explanations.

REQUIREMENTS:
1. Always include the following at the top of your script:
    import clr
    clr.AddReference('RevitAPI')
    clr.AddReference('RevitAPIUI')
    from Autodesk.Revit.DB import *
    from Autodesk.Revit.UI import *
    from Autodesk.Revit.UI.Selection import *
    from System.Collections.Generic import List
    from Autodesk.Revit.DB import ElementId

2. When using uidoc.Selection.SetElementIds(), convert lists to .NET List:
    - WRONG: uidoc.Selection.SetElementIds([x.Id for x in elems])
    - CORRECT: uidoc.Selection.SetElementIds(List[ElementId]([x.Id for x in elems]))

3. Assume the following are pre-defined in scope:
    - __revit__: Active Revit UIApp object
    - uidoc: __revit__.ActiveUIDocument
    - doc: uidoc.Document

4. Do NOT include transaction code. The C# host handles that.

5. Avoid dialogs or UI popups unless requested.

CONTEXT (from documentation):
{context}

---

USER REQUEST:
{user_query}
"""
    return prompt

# Claude call
def call_claude(prompt):
    try:
        client = anthropic.Anthropic(api_key=ANTHROPIC_API_KEY)
        response = client.messages.create(
            model=MODEL_NAME,
            max_tokens=MAX_TOKENS,
            temperature=0.4,
            system="You are a Revit API assistant that returns ONLY working IronPython scripts.",
            messages=[{"role": "user", "content": prompt}]
        )
        return response.content[0].text.strip()
    except Exception as e:
        logging.error(f"Claude generation failed: {e}")
        return "# Error: Failed to generate code from Claude."

# Entry point
if __name__ == "__main__":
    try:
        if len(sys.argv) < 2:
            logging.error("No user query provided.")
            sys.exit(1)

        query = sys.argv[1]

        reused_code = try_reuse_script(query)
        if reused_code:
            print(reused_code)
            sys.exit(0)

        prompt = fallback_rag_prompt(query)
        generated_code = call_claude(prompt)

        if not generated_code or generated_code.strip().startswith("# Error"):
            logging.error(generated_code or "Claude returned empty response.")
            sys.exit(1)

        print(generated_code) 

        # Save successfully generated code
        os.makedirs(success_dir, exist_ok=True)
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        safe_name = "".join(c if c.isalnum() or c in "_-" else "_" for c in query[:50])
        filename = os.path.join(success_dir, f"{timestamp}_{safe_name}.py")

        with open(filename, "w", encoding="utf-8") as f:
            f.write(generated_code)

        logging.info(f"Saved generated code to: {filename}")
        sys.exit(0)

    except Exception as ex:
        logging.exception(f"Fatal error: {ex}")
        sys.exit(1)
