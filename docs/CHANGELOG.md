# Changelog

All notable changes to the `G4.Api` package are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each version heading matches a GitHub release tag without
its `v` prefix and the NuGet package version.

## Unreleased

### Added

- The `G4.Plugins.*` packages 2026.11.25.177 add the `InvokeMiddleClick` and `InvokeUser32MiddleClick` plugins and
  the `Relative` switch of `InvokeScroll`.

### Changed

- `G4.Api` uses the engine packages 2026.11.25.111 and the `G4.Plugins.*` packages 2026.11.25.177, so it resolves
  the same `G4.WebDriver` 2026.11.25.20 and `G4.Abstraction.*` 2026.11.25.26 as the engine.
- The README describes the build instead of linking to a guide outside the repository.

## 2026.11.25.77

### Changed

- Updated the `G4.Plugins.*` packages to 2026.11.25.175, which move `InvokeInputActions` to the Containers toolbox
  group and give the input actions their own Input Actions group.

## 2026.11.24.76

### Changed

- Updated the `G4.Plugins.*` packages to 2026.11.24.174, which add `InvokeDragAndDrop` and
  `InvokeUser32DragAndDrop` and mark the input actions as web only.

## 2026.11.24.75

### Changed

- Updated the engine packages to 2026.11.24.109 and the `G4.Plugins.*` packages to 2026.11.24.173, which add the
  `InvokeInputActions` container and the input action plugins.

## 2026.11.23.74

### Added

- The automation and flow clients raise the `ParallelInvoking` and `ParallelInvoked` events for Parallel containers
  in jobs and stages.

### Changed

- `G4.Api` references the released engine packages 2026.11.23.105 and `G4.Plugins.*` packages 2026.11.23.171.
- The automation and flow clients rethrow a `G4SessionNotCreatedException` to the caller instead of returning an
  empty response.
