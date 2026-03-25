#!/usr/bin/env node

import { ChinesePdfParser } from './index.js';
import { parseArgs } from 'util';

const { values, positionals } = parseArgs({
  args: process.argv.slice(2),
  options: {
    format: { type: 'string', default: 'text' },
    output: { type: 'string' },
    dpi: { type: 'string' },
    'target-pages': { type: 'string' },
    'no-ocr': { type: 'boolean', default: false },
    quiet: { type: 'boolean', default: false }
  },
  allowPositionals: true
});

async function main() {
  const pdfPath = positionals[0];
  
  if (!pdfPath) {
    console.error('Usage: node cli.js <file> [--format json|text] [--output <file>]');
    process.exit(1);
  }
  
  const parser = new ChinesePdfParser();
  
  try {
    const result = await parser.parse(pdfPath);
    const cleaned = parser.cleanResult(result);
    
    if (values.format === 'json') {
      const output = {
        pages: cleaned.pages.map(p => ({
          page: p.pageNum,
          width: p.width,
          height: p.height,
          text: p.text,
          textItems: p.textItems.map(t => ({
            text: t.text,
            x: t.x,
            y: t.y,
            width: t.width,
            height: t.height,
            fontName: t.fontName
          }))
        }))
      };
      console.log(JSON.stringify(output));
    } else {
      for (const page of cleaned.pages) {
        console.log(`--- Page ${page.pageNum} ---\n${page.text}\n`);
      }
    }
  } catch (error) {
    console.error('Error:', error.message);
    process.exit(1);
  }
}

main();