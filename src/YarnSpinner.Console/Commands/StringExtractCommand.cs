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

            // contains every character we have encountered so far
            // this is needed later on for highlighting them
            // also I guess it means we can export info about characters
            HashSet<string> characters = new HashSet<string>();

            // do we have unnmaed characters?
            bool hasUnnamedCharacters = false;

            // used to hold each run of lines that are to be associated together
            List<List<(string id, string text, string character)>> lineBlocks = new List<List<(string id, string text, string character)>>();
            (string id, string text, string character) MakeLineData(string lineID)
            {
                var line = compiledResults.StringTable[lineID];

                string character = string.Empty;
                var index = line.text.IndexOf(':');
                var text = line.text;
                if (index > 0)
                {
                    character = line.text.Substring(0, index);
                    text = line.text.Substring(index + 1).TrimStart();
                    characters.Add(character);
                }
                else if (defaultName != null)
                {
                    character = defaultName;
                    characters.Add(character);
                }
                else
                {
                    hasUnnamedCharacters = true;
                }

                return (id: lineID, text: text, character: character);
            }

            foreach (var node in compiledResults.Program.Nodes)
            {
                var blocks = Yarn.Analysis.InstructionCollectionExtensions.GetBasicBlocks(node.Value);
                var visited = new HashSet<string>();
                foreach (var block in blocks)
                {
                    RunBlock(block, blocks, visited);
                }
            }

            void RunBlock(Yarn.Analysis.BasicBlock block, IEnumerable<Yarn.Analysis.BasicBlock> blocks, HashSet<string> visited, string openingLineID = null)
            {
                if (block.PlayerVisibleContent.Count() == 0)
                {
                    // skipping this block because it has no user content within
                    return;
                }

                if (visited.Contains(block.Name))
                {
                    // we have already visited this one so we can go on without it
                    return;
                }
                visited.Add(block.Name);

                var runOfLines = new List<(string id, string text, string character)>();

                // if we are given an opening line ID we need to add that in at the top
                // this handles the case where we want options to open the set associated lines
                if (!string.IsNullOrEmpty(openingLineID))
                {
                    runOfLines.Add(MakeLineData(openingLineID));
                }

                foreach (var content in block.PlayerVisibleContent)
                {
                    // I really really dislike using objects in this manner
                    // it just feels oh so very strange to me
                    if (content is Yarn.Analysis.BasicBlock.LineElement)
                    {
                        // lines just get added to the current collection of content
                        var line = content as Yarn.Analysis.BasicBlock.LineElement;

                        runOfLines.Add(MakeLineData(line.LineID));
                    }
                    else if (content is Yarn.Analysis.BasicBlock.OptionsElement)
                    {
                        // options are special cased because of how they work
                        // an option will always be put into a block by themselves and any child content they have
                        // so this means we close off the current run of content and add it to the overall container
                        // and then make a new one for each option in the option set
                        if (runOfLines.Count() > 0)
                        {
                            lineBlocks.Add(runOfLines);
                            runOfLines = new List<(string id, string text, string character)>();
                        }

                        var options = content as Yarn.Analysis.BasicBlock.OptionsElement;
                        var jumpOptions = new Dictionary<string, Yarn.Analysis.BasicBlock>();
                        foreach (var option in options.Options)
                        {
                            var destination = blocks.First(block => block.LabelName == option.Destination);
                            if (destination != null && destination.PlayerVisibleContent.Count() > 0)
                            {
                                // there is a valid jump we need to deal with
                                // we store this and will handle it later
                                jumpOptions[option.LineID] = destination;
                            }
                            else
                            {
                                // there is no jump for this option
                                // we just add it to the collection and continue
                                runOfLines.Add(MakeLineData(option.LineID));
                                lineBlocks.Add(runOfLines);
                                runOfLines = new List<(string id, string text, string character)>();
                            }
                        }

                        // now any options without a child block have been handled we need to handle those with children
                        // in that case we want to run through each of those as if they are a new block but with the option at the top
                        foreach (var pair in jumpOptions)
                        {
                            RunBlock(pair.Value, blocks, visited, pair.Key);
                        }
                    }
                    else if (content is Yarn.Analysis.BasicBlock.CommandElement)
                    {
                        // skipping commands as they aren't lines
                        continue;
                    }
                    else
                    {
                        Log.Error("Somehow encountered a non-user facing element...");
                    }
                }

                if (runOfLines.Count() > 0)
                {
                    lineBlocks.Add(runOfLines);
                }
            }

            int lineCount = lineBlocks.Sum(l => l.Count());
            if (lineCount != compiledResults.StringTable.Count())
            {
                Log.Error($"String table has {compiledResults.StringTable.Count()} lines, we encountered {lineCount}!");
            }

            switch (format)
            {
                case "csv":
                {
                    using (var writer = new StreamWriter(location))
                    {
                        // Use the invariant culture when writing the CSV
                        var configuration = new CsvHelper.Configuration.Configuration(System.Globalization.CultureInfo.InvariantCulture);

                        var csv = new CsvHelper.CsvWriter(writer, configuration);
                        
                        // writing out the headers of the table
                        foreach (var column in columns)
                        {
                            csv.WriteField(column);
                        }
                        csv.NextRecord();

                        foreach (var lines in lineBlocks)
                        {
                            foreach (var line in lines)
                            {
                                var character = line.character == string.Empty ? "NO CHAR" : line.character;

                                foreach (var column in columns)
                                {
                                    switch (column.ToLower())
                                    {
                                        case "id":
                                        {
                                            csv.WriteField(line.id);
                                            break;
                                        }
                                        case "text":
                                        {
                                            csv.WriteField(line.text);
                                            break;
                                        }
                                        case "character":
                                        {
                                            csv.WriteField(character);
                                            break;
                                        }
                                        default:
                                        {
                                            csv.WriteField(string.Empty);
                                            break;
                                        }
                                    }
                                }

                                csv.NextRecord();
                            }   
                            // hack to draw a line after each block
                            for (int i = 0; i < columns.Length; i++)
                            {
                                csv.WriteField(string.Empty);
                            }
                            csv.NextRecord();
                        }
                    }

                    break;
                }
                case "xlsx":
                {
                    var wb = new XLWorkbook();
                    var sheet = wb.AddWorksheet("Amazing Dialogue!");
                    int i = 1;

                    // Create the header
                    for (int j = 0; j < columns.Length; j++)
                    {
                        sheet.Cell(i, j + 1).Value = columns[j];
                    }

                    sheet.Row(i).Style.Font.Bold = true; 
                    sheet.Row(i).Style.Fill.BackgroundColor = XLColor.DarkGray;
                    sheet.Row(i).Style.Font.FontColor = XLColor.White;
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

                    i += 1;

                    foreach (var lines in lineBlocks)
                    {
                        foreach (var line in lines)
                        {
                            var character = line.character == string.Empty ? "NO CHAR" : line.character;

                            int j = 1;

                            foreach (var column in columns)
                            {
                                string lineValue;

                                switch (column.ToLower())
                                {
                                    case "id":
                                        lineValue = line.id;
                                        break;
                                    case "text":
                                        lineValue = line.text;
                                        break;
                                    case "character":
                                        lineValue = character;
                                        break;
                                    default:
                                        lineValue = string.Empty;
                                        break;
                                }
                                sheet.Cell(i, j).Value = lineValue;
                                j += 1;
                            }

                            i += 1;
                        }

                        // Add the dividing line between this block and the next
                        sheet.Row(i - 1).Style.Border.SetBottomBorder(XLBorderStyleValues.Thick);
                        sheet.Row(i - 1).Style.Border.SetBottomBorderColor(XLColor.Black);

                        // The next row is twice as high, to create some visual
                        // space between the block we're ending and the next
                        // one.
                        sheet.Row(i).Height = sheet.RowHeight * 2;
                    }

                    // adding the unnamed character into the list of characters so it will also get a colour
                    if (hasUnnamedCharacters)
                    {
                        characters.Add("NO CHAR");
                    }

                    // Wrap the column containing lines, and set it to a
                    // sensible initial width
                    for (int j = 0; j < columns.Length; j++)
                    {
                        if (columns[j].ToLower().Equals("text"))
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
                        sheet.RangeUsed().AddConditionalFormat().WhenIsTrue($"=$A1=\"{character}\"").Fill.SetBackgroundColor(ColorFromHSV(360.0 / characters.Count() * colourIncrementor, random.NextDouble() * range, 1));
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

                    wb.SaveAs(location);
                    break;
                }
            }

            Log.Info("file written");
        }
    }
}