# Yarn Spinner Console

**`ysc`** is the command-line tool for working with [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner) programs.

**NOTE:** This tool is in early preview, and is not ready for production use. If you're new to Yarn Spinner, we recommend starting with the main [Yarn Spinner](https://yarnspinner.dev) package.

## Compiling Scripts

```bash
$ ysc compile [--merge] [--output-directory <output>] <input1.yarn> <input2.yarn> ...
```

`ysc` will compile all of the `.yarn` files you provide, and generate two files for each: a `.yarnc` file containing the compiled file, and a `.csv` containing the extracted string table.

If you specify the `--merge` option, a single `output.yarnc` and `output.csv` file will be created.

## License

`ysc` is available under the [MIT License](LICENSE.md).

## Contributing

See the [Contributing guide](CONTRIBUTING.md) for developer documentation.

