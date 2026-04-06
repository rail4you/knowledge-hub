from setuptools import setup, find_packages

setup(
    name="pageindex-cli",
    version="1.0.0",
    description="CLI to convert PDF/MD/DOCX/PPTX/XLSX to PageIndex JSON node tree via Qwen API",
    py_modules=["pageindex_cli"],
    python_requires=">=3.10",
    install_requires=[
        "litellm>=1.82.0",
        "pymupdf>=1.26.4",
        "PyPDF2>=3.0.1",
        "python-dotenv>=1.1.0",
        "pyyaml>=6.0.2",
        "python-docx>=1.1.0",
        "python-pptx>=1.0.0",
        "openpyxl>=3.1.0",
    ],
    entry_points={
        "console_scripts": [
            "pageindex-cli=pageindex_cli:main",
        ],
    },
)
