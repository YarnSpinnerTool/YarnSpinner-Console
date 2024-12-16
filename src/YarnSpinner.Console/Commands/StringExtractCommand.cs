namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ClosedXML.Excel;
    using Yarn.Compiler;

    public static class StringExtractCommand
    {
        public static void ExtractStrings(FileInfo[] inputs, string format, string[] columns, FileInfo output, string defaultName = null)
        {
            var compiledResults = YarnSpinnerConsole.CompileProgram(inputs);

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (compiledResults.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Aborting string extraction due to errors");
                return;
            }

            if (columns.Length > 0)
            {
                bool hasID = false;
                bool hasText = false;
                foreach (var column in columns)
                {
                    if (column.ToLower().Equals("id"))
                    {
                        hasID = true;
                    }
                    else if (column.ToLower().Equals("text"))
                    {
                        hasText = true;
                    }
                }
                if (!(hasID && hasText))
                {
                    Log.Error("Export requires at least an \"id\" and \"text\" column, aborting.");
                    return;
                }
            }

            string location;
            if (output == null)
            {
                location = $"./lines.{format}";
            }
            else
            {
                location = output.FullName;
            }

            var lineBlocks = Yarn.Compiler.Utility.ExtractStringBlocks(compiledResults.Program.Nodes.Values);
            var stringTable = compiledResults.StringTable;

            bool includeCharacters = true;

            // contains every character we have encountered so far
            // this is needed later on for highlighting them
            // also I guess it means we can export info about characters
            HashSet<string> characters = new HashSet<string>();

            // from here on this is copied and slightly tweaked from the LSP project
            // should probably consider moving that out of the LSP
            StringWriter writer;
            if (format.Equals("csv"))
            {
                writer = new CSVStringWriter(columns, location);
            }
            else
            {
                writer = new ExcelStringWriter(columns);
            }

            foreach (var block in lineBlocks)
            {
                foreach (var lineID in block)
                {
                    var line = stringTable[lineID];

                    string character = defaultName;
                    string text = line.text;
                    if (includeCharacters)
                    {
                        var index = line.text.IndexOf(':');
                        if (index > 0)
                        {
                            character = line.text.Substring(0, index);
                            text = line.text.Substring(index + 1).TrimStart();
                        }
                        characters.Add(character);
                    }

                    foreach (var column in columns)
                    {
                        switch (column)
                        {
                            case "id":
                                writer.WriteColumn(lineID);
                                break;
                            case "text":
                                writer.WriteColumn(text);
                                break;
                            case "character":
                                writer.WriteColumn(character);
                                break;
                            case "line":
                                writer.WriteColumn($"{line.lineNumber}");
                                break;
                            case "file":
                                writer.WriteColumn(line.fileName);
                                break;
                            case "node":
                                writer.WriteColumn(line.nodeName);
                                break;
                            default:
                                writer.WriteColumn(string.Empty);
                                break;
                        }
                    }
                    writer.EndRow();
                }
                writer.EndBlock();
            }
            writer.Format(characters);
            writer.WriteFile(location);

            Log.Info("file written");
        }
    }

    public interface StringWriter
    {
        public void WriteColumn(string value);
        public void EndRow();
        public void EndBlock();
        public void Format(HashSet<string> characters);
        public void WriteFile(string location);
    }
    public class CSVStringWriter: StringWriter
    {
        private string[] columns;
        private StreamWriter stream;
        private CsvHelper.CsvWriter csv;
        public CSVStringWriter(string[] columns, string location)
        {
            this.stream = new StreamWriter(location);
            var configuration = new CsvHelper.Configuration.Configuration(System.Globalization.CultureInfo.InvariantCulture);
            this.csv = new CsvHelper.CsvWriter(stream);
            this.columns = columns;

            foreach (var column in columns)
            {
                this.csv.WriteField(column);
            }
            this.csv.NextRecord();
        }

        public void WriteColumn(string value)
        {
            this.csv.WriteField(value);
        }

        public void EndRow()
        {
            this.csv.NextRecord();
        }

        public void EndBlock()
        {
            for (int i = 0; i < columns.Length; i++)
            {
                this.csv.WriteField(string.Empty);
            }
            this.csv.NextRecord();
        }

        public void Format(HashSet<string> characters) { /* does nothing in CSV */ }

        public void WriteFile(string location)
        {
            this.csv.Flush();

            this.stream.Close();
        }
    }

    public class ExcelStringWriter: StringWriter
    {
        private int rowIndex = 1;
        private int columnIndex = 1;
        private IXLWorksheet sheet;
        private XLWorkbook wb;
        private string[] columns;

        public ExcelStringWriter(string[] columns)
        {
            this.columns = columns;

            wb = new XLWorkbook();
            sheet = wb.AddWorksheet("Amazing Dialogue!");

            // Create the header
            for (int j = 0; j < columns.Length; j++)
            {
                sheet.Cell(rowIndex, j + 1).Value = columns[j];
            }

            sheet.Row(rowIndex).Style.Font.Bold = true; 
            sheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.DarkGray;
            sheet.Row(rowIndex).Style.Font.FontColor = XLColor.White;
            sheet.SheetView.FreezeRows(1);

            // The first column has a border on the right hand side
            sheet.Column("A").Style.Border.SetRightBorder(XLBorderStyleValues.Thick);
            sheet.Column("A").Style.Border.SetRightBorderColor(XLColor.Black);

            // The second column is indent slightly so that it's 
            // not hard up against the border
            sheet.Column("B").Style.Alignment.Indent = 5;

            // The columns always contain text (don't try to infer it to
            // be any other type, like numbers or currency)
            foreach (var col in sheet.Columns())
            {
                col.DataType = XLDataType.Text;
                col.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            rowIndex += 1;
        }

        public void WriteColumn(string value)
        {
            sheet.Cell(rowIndex, columnIndex).Value = value;
            columnIndex += 1;
        }

        public void EndRow()
        {
            rowIndex += 1;
            columnIndex = 1;
        }

        public void EndBlock()
        {
            // Add the dividing line between this block and the next
            sheet.Row(rowIndex - 1).Style.Border.SetBottomBorder(XLBorderStyleValues.Thick);
            sheet.Row(rowIndex - 1).Style.Border.SetBottomBorderColor(XLColor.Black);

            // The next row is twice as high, to create some visual
            // space between the block we're ending and the next
            // one.
            sheet.Row(rowIndex).Height = sheet.RowHeight * 2;
        }

        public void Format(HashSet<string> characters)
        {
            // Wrap the column containing lines, and set it to a
            // sensible initial width
            for (int j = 0; j < columns.Length; j++)
            {
                if (columns[j].Equals("text"))
                {
                    sheet.Column(j + 1).Style.Alignment.WrapText = true;
                    sheet.Column(j + 1).Width = 80;
                    break;
                }
            }

            // colouring every character
            // we do this by moving around the hue wheel and a 20-40% saturation
            // this creates a mostly low collision colour for labelling characters
            int colourIncrementor = 0;
            Random random = new Random();
            double range = (0.4 - 0.2) + 0.2; // putting this out here so I can tweak it as needed: (max - min) + min
            foreach (var character in characters)
            {
                sheet.RangeUsed().AddConditionalFormat().WhenIsTrue($"=$A1=\"{character}\"").Fill.SetBackgroundColor(ColorFromHSV(360.0 / characters.Count * colourIncrementor, random.NextDouble() * range, 1));
                colourIncrementor += 1;
            }

            XLColor ColorFromHSV(double hue, double saturation, double value)
            {
                int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
                double f = (hue / 60) - Math.Floor(hue / 60);

                value = value * 255;
                int v = Convert.ToInt32(value);
                int p = Convert.ToInt32(value * (1 - saturation));
                int q = Convert.ToInt32(value * (1 - f * saturation));
                int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

                switch (hi)
                {
                    case 0:
                        return XLColor.FromArgb(255, v, t, p);
                    case 1:
                        return XLColor.FromArgb(255, q, v, p);
                    case 2:
                        return XLColor.FromArgb(255, p, v, t);
                    case 3:
                        return XLColor.FromArgb(255, p, q, v);
                    case 4:
                        return XLColor.FromArgb(255, t, p, v);
                    default:
                        return XLColor.FromArgb(255, v, p, q);
                }
            }
        }

        public void WriteFile(string location)
        {
            wb.SaveAs(location);
        }
    }
}
