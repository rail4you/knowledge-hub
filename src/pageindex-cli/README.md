# PageIndex CLI

Convert **PDF, Markdown, DOCX, PPTX, XLSX** into a **PageIndex JSON node tree** using the Qwen API (or any OpenAI-compatible endpoint).

Built on top of [VectifyAI/PageIndex](https://github.com/VectifyAI/PageIndex) — a vectorless, reasoning-based RAG framework.

---

## 📦 Installation

### 1. Clone with PageIndex
```bash
git clone https://github.com/VectifyAI/PageIndex.git
# Place pageindex_cli.py alongside the PageIndex/ folder
```

### 2. Install dependencies
```bash
pip install -r requirements.txt
```

### 3. (Optional) Install as a CLI command
```bash
pip install -e .
# Now you can use: pageindex-cli
```

---

## 🚀 Quick Start

```bash
# PDF — using Qwen API (DashScope)
python pageindex_cli.py document.pdf \
    --qwen-api-key sk-xxxxxxxx \
    --qwen-model qwen-long

# DOCX
python pageindex_cli.py report.docx \
    --qwen-api-key sk-xxxxxxxx \
    --qwen-model qwen-plus

# PPTX
python pageindex_cli.py slides.pptx \
    --qwen-api-key sk-xxxxxxxx

# XLSX
python pageindex_cli.py data.xlsx \
    --qwen-api-key sk-xxxxxxxx

# Markdown
python pageindex_cli.py notes.md \
    --qwen-api-key sk-xxxxxxxx
```

---

## 🔑 API Configuration

### Option A — CLI flags
```bash
pageindex-cli doc.pdf \
  --qwen-api-key  sk-xxxxxxxx \
  --qwen-api-base https://dashscope.aliyuncs.com/compatible-mode/v1 \
  --qwen-model    qwen-long
```

### Option B — Environment variables
```bash
export QWEN_API_KEY=sk-xxxxxxxx
export QWEN_API_BASE=https://dashscope.aliyuncs.com/compatible-mode/v1
export QWEN_MODEL=qwen-long
pageindex-cli doc.pdf
```

### Option C — `.env` file
```dotenv
QWEN_API_KEY=sk-xxxxxxxx
QWEN_API_BASE=https://dashscope.aliyuncs.com/compatible-mode/v1
QWEN_MODEL=qwen-long
```
```bash
pageindex-cli doc.pdf  # auto-loads .env
```

### Qwen Model Reference
| Model | Best for |
|-------|----------|
| `qwen-long` | Long documents (up to 10M tokens) ✅ recommended |
| `qwen-max` | Highest reasoning quality |
| `qwen-plus` | Balanced cost/quality |
| `qwen-turbo` | Fast & cheap |

---

## 📖 All Options

```
usage: pageindex-cli [-h] [-o FILE]
                     [--qwen-api-key KEY] [--qwen-api-base URL] [--qwen-model MODEL]
                     [--toc-check-pages N] [--max-pages-per-node N] [--max-tokens-per-node N]
                     [--no-summary] [--no-description] [--include-text] [--no-node-id]
                     [--thinning] [--thinning-threshold N] [--summary-token-threshold N]
                     [--keep-temp] [--compact]
                     FILE

Positional:
  FILE                        Input file (PDF / MD / DOCX / PPTX / XLSX)

Output:
  -o, --output FILE           Output JSON path (default: <input_stem>_index.json)

API Configuration:
  --qwen-api-key KEY          Qwen API key
  --qwen-api-base URL         API base URL (default: DashScope compatible endpoint)
  --qwen-model MODEL          Model name (default: qwen-long)

PageIndex Options:
  --toc-check-pages N         Pages to scan for TOC, PDF only (default: 20)
  --max-pages-per-node N      Max pages per tree node, PDF only (default: 10)
  --max-tokens-per-node N     Max tokens per node, PDF only (default: 20000)
  --no-summary                Skip per-node summaries (faster, smaller output)
  --no-description            Skip document-level description
  --include-text              Embed raw text in each node (larger output)
  --no-node-id                Omit node IDs

Markdown / Office Options:
  --thinning                  Merge small nodes (Markdown/Office only)
  --thinning-threshold N      Min tokens before thinning (default: 5000)
  --summary-token-threshold N Min node tokens before summarizing (default: 200)

Misc:
  --keep-temp                 Keep temp .md file created from DOCX/PPTX/XLSX
  --compact                   Compact JSON (no indentation)
```

---

## 📄 Output Format

The output is a JSON tree matching the PageIndex schema:

```json
{
  "doc_name": "Annual Report 2024",
  "doc_description": "A comprehensive overview of...",
  "structure": [
    {
      "title": "Executive Summary",
      "node_id": "0001",
      "start_index": 1,
      "end_index": 4,
      "summary": "Key highlights include...",
      "nodes": [
        {
          "title": "Financial Highlights",
          "node_id": "0002",
          "start_index": 2,
          "end_index": 4,
          "summary": "Revenue grew 23% YoY..."
        }
      ]
    }
  ],
  "_meta": {
    "source_file": "/path/to/report.pdf",
    "source_format": "PDF",
    "model": "qwen-long",
    "api_base": "https://dashscope.aliyuncs.com/compatible-mode/v1"
  }
}
```

### How Office Formats Are Handled

| Format | Conversion Strategy |
|--------|-------------------|
| **DOCX** | Paragraphs with `Heading N` styles → `# N` Markdown headings; tables → MD tables |
| **PPTX** | Each slide → `## Slide Title` section; body text as content |
| **XLSX** | Each sheet → `## Sheet Name` section; data as Markdown table |

All formats are first converted to a temporary Markdown file, which PageIndex then indexes. Use `--keep-temp` to inspect the intermediate `.md` file.

---

## 🔗 Related

- [PageIndex GitHub](https://github.com/VectifyAI/PageIndex)
- [Qwen API (DashScope)](https://dashscope.aliyuncs.com)
- [LiteLLM Docs](https://docs.litellm.ai)
