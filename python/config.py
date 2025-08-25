# config.py

import os
import torch

# === API + Model ===
EMBEDDER_MODEL = "nomic-ai/nomic-embed-text-v1.5"
EMBEDDER_TRUST_REMOTE_CODE = True
EMBEDDER_DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

MODEL_NAME = "claude-opus-4-20250514"
ANTHROPIC_API_KEY = "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"

MAX_TOKENS = 1024
TEMPERATURE = 0.4
 

