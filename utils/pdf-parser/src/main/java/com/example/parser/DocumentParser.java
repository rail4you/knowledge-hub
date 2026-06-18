package com.example.parser;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.apache.poi.xwpf.extractor.XWPFWordExtractor;
import org.apache.poi.xwpf.usermodel.XWPFDocument;
import org.apache.poi.xslf.usermodel.XMLSlideShow;
import org.apache.poi.xslf.usermodel.XSLFSlide;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;
import org.apache.poi.ss.usermodel.*;
import org.opendataloader.pdf.api.OpenDataLoaderPDF;
import org.opendataloader.pdf.api.Config;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/**
 * Universal document parser that handles PDF (via OpenDataLoader),
 * DOCX, PPTX, and XLSX (via Apache POI).
 * 
 * Usage: java com.example.parser.DocumentParser <inputFile> <outputDir>
 * 
 * Output: Writes a JSON file to outputDir with structure:
 * {
 *   "number of pages": N,
 *   "kids": [
 *     { "type": "paragraph", "page number": 1, "content": "..." },
 *     ...
 *   ]
 * }
 */
public class DocumentParser {

    private static final ObjectMapper mapper = new ObjectMapper();

    public static void main(String[] args) throws Exception {
        if (args.length < 2) {
            System.err.println("Usage: java com.example.parser.DocumentParser <inputFile> <outputDir>");
            System.exit(1);
        }

        String inputFile = args[0];
        String outputDir = args[1];

        File in = new File(inputFile);
        if (!in.exists()) {
            System.err.println("Input file not found: " + inputFile);
            System.exit(1);
        }

        String name = in.getName().toLowerCase(Locale.ROOT);
        ObjectNode result;

        if (name.endsWith(".pdf")) {
            result = parsePdf(in, outputDir);
        } else if (name.endsWith(".docx")) {
            result = parseDocx(in);
        } else if (name.endsWith(".pptx")) {
            result = parsePptx(in);
        } else if (name.endsWith(".xlsx")) {
            result = parseXlsx(in);
        } else {
            System.err.println("Unsupported file format: " + name);
            System.exit(1);
            return;
        }

        // Write output JSON
        String outputFileName = in.getName().replaceAll("\\.[^.]+$", "") + ".json";
        File outputFile = new File(outputDir, outputFileName);
        mapper.writerWithDefaultPrettyPrinter().writeValue(outputFile, result);
        
        System.out.println("Successfully parsed " + in.getName());
        System.out.println("Output: " + outputFile.getAbsolutePath());
    }

    /**
     * Parse PDF using OpenDataLoader.
     */
    private static ObjectNode parsePdf(File input, String outputDir) throws Exception {
        Config config = new Config();
        config.setGenerateJSON(true);
        config.setGenerateText(false);
        config.setGenerateHtml(false);
        config.setGenerateMarkdown(false);
        config.setKeepLineBreaks(true);
        config.setOutputFolder(outputDir);

        OpenDataLoaderPDF.processFile(input.getAbsolutePath(), config);

        // OpenDataLoader writes output to a JSON file in the outputDir
        // Find the generated JSON file
        String baseName = input.getName().replaceAll("\\.[^.]+$", "");
        File[] jsonFiles = new File(outputDir).listFiles((dir, f) -> 
            f.startsWith(baseName) && f.endsWith(".json"));
        
        if (jsonFiles != null && jsonFiles.length > 0) {
            return (ObjectNode) mapper.readTree(jsonFiles[0]);
        }

        // Fallback: look for any JSON file
        File[] anyJson = new File(outputDir).listFiles((dir, f) -> f.endsWith(".json"));
        if (anyJson != null && anyJson.length > 0) {
            return (ObjectNode) mapper.readTree(anyJson[0]);
        }

        // Empty result
        ObjectNode empty = mapper.createObjectNode();
        empty.put("number of pages", 0);
        empty.set("kids", mapper.createArrayNode());
        return empty;
    }

    /**
     * Parse DOCX using Apache POI XWPF.
     */
    private static ObjectNode parseDocx(File input) throws Exception {
        ObjectNode result = mapper.createObjectNode();
        ArrayNode kids = mapper.createArrayNode();

        try (XWPFDocument doc = new XWPFDocument(new FileInputStream(input))) {
            // Use extractor to get all text
            XWPFWordExtractor extractor = new XWPFWordExtractor(doc);
            String fullText = extractor.getText();
            extractor.close();

            // DOCX doesn't have native page concept - treat as single page
            String[] paragraphs = fullText.split("\n");
            int pageNum = 1;

            for (String para : paragraphs) {
                String trimmed = para.trim();
                if (trimmed.isEmpty()) continue;

                ObjectNode kid = mapper.createObjectNode();
                kid.put("type", "paragraph");
                kid.put("page number", pageNum);
                kid.put("content", trimmed);
                kids.add(kid);
            }
        }

        result.put("number of pages", 1);
        result.set("kids", kids);
        return result;
    }

    /**
     * Parse PPTX using Apache POI XSLF.
     */
    private static ObjectNode parsePptx(File input) throws Exception {
        ObjectNode result = mapper.createObjectNode();
        ArrayNode kids = mapper.createArrayNode();

        try (XMLSlideShow ppt = new XMLSlideShow(new FileInputStream(input))) {
            List<XSLFSlide> slides = ppt.getSlides();
            
            for (int i = 0; i < slides.size(); i++) {
                XSLFSlide slide = slides.get(i);
                int pageNum = i + 1;

                // Extract text from all shapes on the slide
                slide.getShapes().forEach(shape -> {
                    if (shape instanceof org.apache.poi.xslf.usermodel.XSLFTextShape) {
                        String text = ((org.apache.poi.xslf.usermodel.XSLFTextShape) shape).getText();
                        if (text != null && !text.trim().isEmpty()) {
                            // Split multi-line text into separate paragraphs
                            String[] lines = text.split("\n");
                            for (String line : lines) {
                                String trimmed = line.trim();
                                if (trimmed.isEmpty()) continue;

                                ObjectNode kid = mapper.createObjectNode();
                                kid.put("type", "paragraph");
                                kid.put("page number", pageNum);
                                kid.put("content", trimmed);
                                kids.add(kid);
                            }
                        }
                    }
                });
            }
        }

        result.put("number of pages", kids.size() > 0 ? 
            kids.get(kids.size() - 1).get("page number").asInt() : 0);
        result.set("kids", kids);
        return result;
    }

    /**
     * Parse XLSX using Apache POI XSSF.
     */
    private static ObjectNode parseXlsx(File input) throws Exception {
        ObjectNode result = mapper.createObjectNode();
        ArrayNode kids = mapper.createArrayNode();
        int pageNum = 1;

        try (XSSFWorkbook workbook = new XSSFWorkbook(new FileInputStream(input))) {
            for (int i = 0; i < workbook.getNumberOfSheets(); i++) {
                Sheet sheet = workbook.getSheetAt(i);
                String sheetName = sheet.getSheetName();

                // Add sheet name as a heading
                ObjectNode heading = mapper.createObjectNode();
                heading.put("type", "heading");
                heading.put("page number", pageNum);
                heading.put("content", "Sheet: " + sheetName);
                kids.add(heading);

                // Process each row
                for (Row row : sheet) {
                    StringBuilder rowText = new StringBuilder();
                    for (Cell cell : row) {
                        if (rowText.length() > 0) rowText.append("\t");
                        rowText.append(getCellValue(cell));
                    }
                    String trimmed = rowText.toString().trim();
                    if (!trimmed.isEmpty()) {
                        ObjectNode kid = mapper.createObjectNode();
                        kid.put("type", "table");
                        kid.put("page number", pageNum);
                        kid.put("content", trimmed);
                        kids.add(kid);
                    }
                }
            }
        }

        result.put("number of pages", pageNum);
        result.set("kids", kids);
        return result;
    }

    private static String getCellValue(Cell cell) {
        if (cell == null) return "";
        return switch (cell.getCellType()) {
            case STRING -> cell.getStringCellValue();
            case NUMERIC -> {
                double val = cell.getNumericCellValue();
                if (val == Math.floor(val) && !Double.isInfinite(val)) {
                    yield String.valueOf((long) val);
                }
                yield String.valueOf(val);
            }
            case BOOLEAN -> String.valueOf(cell.getBooleanCellValue());
            case FORMULA -> {
                try {
                    yield String.valueOf(cell.getNumericCellValue());
                } catch (Exception e) {
                    try {
                        yield cell.getStringCellValue();
                    } catch (Exception e2) {
                        yield cell.getCellFormula();
                    }
                }
            }
            case BLANK -> "";
            default -> "";
        };
    }
}
