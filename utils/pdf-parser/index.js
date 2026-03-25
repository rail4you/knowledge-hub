import { createRequire } from 'module';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import { readFileSync } from 'fs';
import * as pdfjsLib from 'pdfjs-dist';

const __dirname = dirname(fileURLToPath(import.meta.url));

export class ChinesePdfParser {
  constructor(options = {}) {
    this.cMapUrl = options.cMapUrl || join(__dirname, 'node_modules/pdfjs-dist/cmaps/');
    this.standardFontDataUrl = options.standardFontDataUrl || join(__dirname, 'node_modules/pdfjs-dist/standard_fonts/');
    
    pdfjsLib.GlobalWorkerOptions.workerSrc = join(__dirname, 'node_modules/pdfjs-dist/build/pdf.worker.mjs');
  }

  async parse(pdfPath) {
    const data = new Uint8Array(readFileSync(pdfPath));
    
    const loadingTask = pdfjsLib.getDocument({
      data,
      cMapUrl: this.cMapUrl,
      cMapPacked: true,
      standardFontDataUrl: this.standardFontDataUrl,
    });
    
    const pdf = await loadingTask.promise;
    const result = {
      numPages: pdf.numPages,
      metadata: await pdf.getMetadata(),
      pages: []
    };
    
    for (let i = 1; i <= pdf.numPages; i++) {
      const page = await pdf.getPage(i);
      const textContent = await page.getTextContent();
      const viewport = page.getViewport({ scale: 1.0 });
      
      const textItems = textContent.items.map(item => ({
        text: item.str,
        x: item.transform[4],
        y: viewport.height - item.transform[5],
        width: item.width,
        height: item.height,
        fontName: item.fontName,
      }));
      
      result.pages.push({
        pageNum: i,
        width: viewport.width,
        height: viewport.height,
        text: textItems.map(t => t.text).join(''),
        textItems
      });
    }
    
    return result;
  }

  async parseToText(pdfPath) {
    const result = await this.parse(pdfPath);
    return result.pages.map(p => p.text).join('\n\n--- Page ---\n\n');
  }

  cleanText(text) {
    let cleaned = text;
    
    cleaned = this.removePrivateUseAreaChars(cleaned);
    cleaned = this.removeGarbledPatterns(cleaned);
    cleaned = this.normalizeWhitespace(cleaned);
    cleaned = this.removeControlCharacters(cleaned);
    
    return cleaned;
  }

  removePrivateUseAreaChars(text) {
    return text.replace(/[\uE000-\uF8FF]/g, '');
  }

  removeGarbledPatterns(text) {
    let cleaned = text;
    
    cleaned = cleaned.replace(/檿+/g, '');
    cleaned = cleaned.replace(/殨+/g, '');
    cleaned = cleaned.replace(/^\s*[\u4E00-\u9FFF]+\s*$/gm, '');
    
    return cleaned;
  }

  normalizeWhitespace(text) {
    let cleaned = text;
    
    cleaned = cleaned.replace(/([\u4E00-\u9FFF])\s{2,}([\u4E00-\u9FFF])/g, '$1$2');
    
    cleaned = cleaned.replace(/[ \t]+/g, ' ');
    
    cleaned = cleaned.replace(/\n{3,}/g, '\n\n');
    
    cleaned = cleaned.replace(/^\s+|\s+$/gm, '');
    
    return cleaned;
  }

  removeControlCharacters(text) {
    return text.replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, '');
  }

  cleanPage(page) {
    return {
      ...page,
      text: this.cleanText(page.text),
      textItems: page.textItems.map(item => ({
        ...item,
        text: this.cleanText(item.text)
      }))
    };
  }

  cleanResult(result) {
    return {
      ...result,
      pages: result.pages.map(p => this.cleanPage(p))
    };
  }
}

async function main() {
  const args = process.argv.slice(2);
  
  if (args.length === 0) {
    console.log('Usage: node index.js <pdf-path>');
    console.log('Example: node index.js /path/to/chinese.pdf');
    process.exit(1);
  }
  
  const pdfPath = args[0];
  const parser = new ChinesePdfParser();
  
  console.log(`Parsing: ${pdfPath}\n`);
  
  try {
    const result = await parser.parse(pdfPath);
    const cleaned = parser.cleanResult(result);
    
    console.log(`Pages: ${cleaned.numPages}`);
    console.log(`\n=== Content (cleaned) ===\n`);
    
    for (const page of cleaned.pages) {
      console.log(`--- Page ${page.pageNum} ---`);
      console.log(page.text);
      console.log();
    }
  } catch (error) {
    console.error('Error:', error.message);
    process.exit(1);
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main();
}