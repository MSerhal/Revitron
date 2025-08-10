# config.py

import os

# === API + Model ===
USE_EMBEDDER_MODEL = True  # Set to False if not using embedding model
MODEL_PROVIDER = "gemini"  # Options: "claude", "gemini", "openai"
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY") or "your-gemini-api-key"
CLAUDE_API_KEY = os.getenv("CLAUDE_API_KEY") or "your-claude-key"
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY") or "your-openai-key"

# === Paths ===
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(BASE_DIR, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

# File where generated IronPython code will be saved (optional)
SAVE_GENERATED_CODE = True
GENERATED_CODE_FILENAME_FORMAT = "{timestamp}_{task_name}.py"

# === Logging ===
ENABLE_LOGGING = True
LOG_FILE = os.path.join(BASE_DIR, "copilot.log")

# === Prompt Embedding Config ===
EMBEDDING_MODEL = "snowflake"  # or "sentence-transformers"
VECTOR_STORE_PATH = os.path.join(BASE_DIR, "vector_index")

# === Misc ===
DEBUG = True
VERBOSE = True
