# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Added support for using .yarnproject files on all commands.
- Added the `list-sources` command, which lists all .yarn files used by a Yarn Project.
- Added the `browse-binary` command, which dumps out all nodes, their headers, and declared variables embedded within the compiled Yarn program. 

### Changed

### Removed

## [2.3.1] 2023-03-6

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
