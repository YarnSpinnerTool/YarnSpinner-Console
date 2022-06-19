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

## Compiling Scripts

```bash
$ ysc compile [--merge] [--output-directory <output>] [--use-input-relative-paths] <input1.yarn> <input2.yarn> ...
```

`ysc` will compile all of the `.yarn` files you provide, and generate two files for each: a `.yarnc` file containing the compiled file, and a `.csv` containing the extracted string table.

If you specify the `--merge` option, a single `output.yarnc` and `output.csv` file will be created.

If you specify the `--use-input-relative-paths` option, output files will use relative paths for yarn files rather than absolute paths.

## Running Scripts

```bash
$ ysc run [--auto-advance] [--start-node <Start>] <input1.yarn> <input2.yarn> ...
```

`ysc` will compile all of the `.yarn` files provided and then begin running them from the `Start` node.
By specifiying the `--start-node` option you can configure which node is used as the entry point for the story, defaulting to `Start` if not set.

If you specify the `--auto-advance` flag, the normal lines of dialogue will be presented automatically, only holding the program up when an option or shortcut is reached.
This flag is not set by default meaning each line of dialogue will halt the story until manually advanced with the `return`/`enter` key.

**NOTE:** Custom functions are not supported and encountering one will cause the story to be aborted.

## Upgrading Scripts

```bash
$ ysc upgrade [--upgrade-type <1>] <input1.yarn> <input2.yarn> ...
```

`ysc` will upgrade the `yarn` files from one version of Yarn to another.
By specifying the `--upgrade-type` option you can configure from what version, to which version, of Yarn to convert.
Defaults to `1`, or upgrading a yarn v1 file to a yarn v2 file.

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

## License

`ysc` is available under the [MIT License](LICENSE.md).

## Contributing

See the [Contributing guide](CONTRIBUTING.md) for developer documentation.
