# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

### Changed

### Removed

## [3.0.0-beta2] 2025-03-05

### Added

- Added a new command, `dump-code`, which compiles Yarn files or a Yarn Project and outputs a human-readable version of the compiled bytecode to stdout. This is intended for developers who are working on tools that consume compiled Yarn files.

### Changed

- The `--allow-preview-features` (`-p`) flag can now be used when compiling scripts to enable preview features.
- .ysls.json files referenced by Yarn Project files are now parsed and used when compiling Yarn code.

### Removed

## [2.5.0] 2024-12-16

### Changed

- The metadata CSV file now stores its metadata in the same way as Yarn Spinner for Unity's export feature, with a single `metadata` column that contains a space-separated list of tags.

## [2.4.2] 2024-02-15

### Changed

- Updated to Yarn Spinner 2.4.2.

## [2.4.1] 2023-11-21

### Changed

- `project-name` now has a `--unity-exclusion` flag which sets the exclusion field to default more appropriate for Unity.
- Fixed a bug where the `visited` and `visit_count` functions were declared twice.

## [2.4.0] 2023-11-15

### Added

- Added a `create-proj` command which will create a new Yarn Project file.

### Changed

- The `run` command will now correctly abort when asked to run invalid Yarn.
- A compiler error will no longer return a 0 exit code.

## [2.3.2] 2023-07-07

### Added

- Added support for using .yarnproject files on all commands.
- Added the `list-sources` command, which lists all .yarn files used by a Yarn Project.
- Added the `browse-binary` command, which dumps out all nodes, their headers, and declared variables embedded within the compiled Yarn program. 

## [2.3.1] 2023-03-06

### Added

- New command `version` shows the current version of the tool, and of Yarn Spinner itself.

### Changed

- Updated to [Yarn Spinner v2.3.0](https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.3.0).

## [2.3.0] 2022-10-31

### Added

- New subcommand `extract` allows for extraction of lines for the purpose of VO.
- New subcommand `graph` allows for exporting a dot graph file of your dialogue.

### Changed

- `upgrade` now does a compile after upgrade to catch some sitations that upgrade but are still invalid.
- `compile` now has a simpler and more useful naming option structure.
- `tag` now aborts tagging on malformed Yarn files, this prevents some unexpected behaviour.

## [2.2.0] 2022-04-27

### Changed

- Updated to use Yarn Spinner 2.2.0. See release notes: https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.2.0

## [2.1.0] 2022-02-17

### Changed

- Updated to use Yarn Spinner 2.1.0. See release notes: https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.1.0

## [2.0.2] 2022-02-08

### Changed

- Updated to use Yarn Spinner 2.0.2. See release notes: https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.0.2

## [2.0.1] 2021-12-23

### Added

### Changed

- Updated to use Yarn Spinner 2.0.1. See release notes: https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.0.1

### Removed


## [2.0.0] 2021-12-18

Initial release of ysc.
