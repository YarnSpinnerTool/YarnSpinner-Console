# Yarn Spinner Console

**`ysc`** is the command-line tool for working with [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner) programs.

## Installing `ysc`

You can install `ysc` by downloading the [most recent release](https://github.com/YarnSpinnerTool/YarnSpinner-Console/releases/latest), or by building it locally.

<details>
<summary>Building it locally</summary>
<p>

* Download and install the [.NET SDK](https://dotnet.microsoft.com/en-us/download).
* In your terminal, build and run the project with the following command:

    ```bash
    dotnet-run -- [your commands]
    ```

    For example, to compile a Yarn script, run the following command:

    ```bash
    dotnet-run -- compile path/to/MyScript.yarn
    ```

</p>
</details>

## Running Scripts

```bash
$ ysc run [--auto-advance] [--start-node <Start>] <input1.yarn> <input2.yarn> ...
```

`ysc` will compile all of the `.yarn` files provided and then begin running them from the `Start` node.
By specifiying the `--start-node` option you can configure which node is used as the entry point for the story, defaulting to `Start` if not set.

If you specify the `--auto-advance` flag, the normal lines of dialogue will be presented automatically, only holding the program up when an option or shortcut is reached.
This flag is not set by default meaning each line of dialogue will halt the story until manually advanced with the `return`/`enter` key.

**NOTE:** Custom functions are not supported and encountering one will cause the story to be aborted.

## Compiling Scripts

```bash
$ ysc compile [--output-directory <output>] [--output-name <name>] [--output-string-table-name <tablename>] [--output-metadata-table-name <metadataname>] <input1.yarn> <input2.yarn> ...
```

`ysc` will compile all of the `.yarn` files you provide, and generates three files: `input.yarnc` compiled program, `input-Lines.csv` strings table, and `input-Metadata.csv` table of line metadata.

By default the name of the yarn file will be used to name the compiled output.
If more than one Yarn file is included then you can set a name using the `--output-name` option, this name will then be used as the base name for the files.
If a name isn't set and there are more than one input, the default name of `Output` will be used as the base name.

If further customisation `--output-string-table-name` and `--output-metadata-table-name` allow overriding the filename of the string and metadata tables respectively.

## Listing Sources

```bash
$ ysc list-sources <input.yarnproject>
```

Lists all of the yarn files that are associated with the `input.yarnproject` Yarn Project.
This reads both the includes and excludes of the Yarn Project and will work out what files match those filters.
You can use this to make sure you have set your globstar values correctly.

## Printing the Syntax Tree

```bash
$ ysc print-tree [--output-directory <output>] [--json] <input1.yarn> <input2.yarn> ...
```

Prints a human readable form of the dialogue syntax tree of the input Yarn files.
This is useful when debugging the language itself or for some more unusual shenanigans.
Defaults to returning the syntax tree as a text file, by specifiying the `--json` flag will instead return them as a JSON file.

## Printing the Parser Tokens

```bash
$ ysc print-tokens [--output-directory <output>] [--json] <input1.yarn> <input2.yarn> ...
```

Prints a list of all parser tokens from the included Yarn files.
Tokens are shown with their line number and starting index.
This is useful when debugging the language itself or for some more unusual shenanigans.
Defaults to returning the syntax tree as a text file, by specifiying the `--json` flag will instead return them as a JSON file.

## Tagging lines for localisation

```bash
$ ysc tag [--output-directory <output>] <input1.yarn> <input2.yarn> ...
```

Tags the input Yarn files with line ID hashtag for localisation.
Uses the line tagging code from YarnSpinner core, this means by default lines will be tagged following the rules of tagging lines from the core.
If `--output-directory` is not set will default to overriding the files in place.

## Extracting lines for recording

```bash
$ ysc extract <input1.yarn> <input2.yarn> ... [--format csv|xlsx] [--columns <column1> <column2> ...] [--default-name <name>] [--output <file>]
```

Creates a tables of all lines in the included Yarn files in a format intended for easier recording.
Runs of lines are collected and seperated in the table.
Currently the table shows the character, the line, and the line ID in that order.

Defaults to extracting the strings as a csv but this can be changed using the `--format` option.
If the excel option is set (`--format xlsx`) then conditional highlighting will be used to colour each characters lines.

If the `columns` option is set you can define a list of columns you want the output to have.
There are several pre-defined columns (`text`, `id`, `character`, `file`, `line`, `node`) which can be used and the exporter will fill in the appropriate line details in that place.
Any custom columns are left blank.
If setting the columns you must include at least `text` and `id` somewhere in your for the extraction to continue.
If `--columns` is not set it will default to using the columns `character`, `text`, `id` in that order.

If the `default-name` option is set you can define a default name for lines of dialogue that do not have a character set.
Defaults to none if not used.

If the `output` option is set you can define a file location for the extracted dialogue.
When exporting an `xlsx` the file type of set when using `--output` *must* be `xlsx` or else the export will fail.
Defaults to `lines` in the current directory if not set.

## Generating a node graph

```bash
$ ysc graph <input1.yarn> <input2.yarn> ... [--clustering] [--format dot|mermaid] [--output <file>]
```

Creates a graph in the [DOT](https://graphviz.org/doc/info/lang.html) or [mermaid](https://mermaid-js.github.io/mermaid/) graph description language of all nodes and their links in the Yarn files.
This allows for a high level look at the structure of the story.
If positional information is contained with the header of hodes this will also be captured in the output where possible.

If the `output` option is set you can define a file location for the graph.
By default will name itself `dialogue` if not otherwise set.
The default file extension will change depending on the `format`, using `.dot` and `.mmd` for DOT and mermaid graphs respectively.

If the `clustering` flag is set the graph will cluster nodes into subgraphs based on the file they are contained within.
This may or may not be of use depending on the visualisation tool used to render the graph.

The `format` option can be set to determine the graph format.
Can be either `dot` or `mermaid`.
Will default to `dot` if not otherwise set.

Note that this generates the graph file itself, to preview it you will need a tool that can import and visualise DOT or mermaid files.

## Browsing Compiled Binary

```bash
$ ysc browse-binary <input.yarnproject>
```

Presents common information inside of the compiled `input.yarnproject`.
Displays all nodes and their headers, and all variables declarations and their default values.

## Creating Yarn Project

```bash
$ ysc create-proj [--unity-exclusion] <project-name> 
```

Creates a new default Yarn Project named `project-name`.
Defaults to setting no exclusions, if the project is intended to be used in Unity setting the `--unity-exclusion` flag will exclude folders with a trailing ~ which is what Unity expects.

## License

`ysc` is available under the [MIT License](LICENSE.md).

## Contributing

See the [Contributing guide](CONTRIBUTING.md) for developer documentation.

