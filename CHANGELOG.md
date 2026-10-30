# Changelog

## [2.4.0](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.3.0...v2.4.0) (2026-10-30)


### Features

* regenerate from puntjes-openapi v2.4.0 ([#29](https://github.com/PuntjesApp/puntjes-dotnet/issues/29)) ([183bd16](https://github.com/PuntjesApp/puntjes-dotnet/commit/183bd1668b4b2715d8c2ff772d129385c176ea53))

## [2.3.0](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.2.3...v2.3.0) (2026-10-26)


### Features

* regenerate from puntjes-openapi v2.3.0 ([#27](https://github.com/PuntjesApp/puntjes-dotnet/issues/27)) ([f860bbb](https://github.com/PuntjesApp/puntjes-dotnet/commit/f860bbb3b24881981574d430ad389ee67c759637))

## [2.2.3](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.2.2...v2.2.3) (2026-10-26)


### Bug Fixes

* keep the number of each model enum member across releases ([#24](https://github.com/PuntjesApp/puntjes-dotnet/issues/24)) ([016e67b](https://github.com/PuntjesApp/puntjes-dotnet/commit/016e67b797e3e412062205dcb5d5dd12ad5e7a33))

## [2.2.2](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.2.1...v2.2.2) (2026-10-26)


### Bug Fixes

* keep the number of each ErrorCode across releases ([#22](https://github.com/PuntjesApp/puntjes-dotnet/issues/22)) ([07d8901](https://github.com/PuntjesApp/puntjes-dotnet/commit/07d8901e551e280225efca78c5635d477ff491f7))

## [2.2.1](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.2.0...v2.2.1) (2026-10-18)


### Bug Fixes

* make an optional request body optional again ([#16](https://github.com/PuntjesApp/puntjes-dotnet/issues/16)) ([1ce9653](https://github.com/PuntjesApp/puntjes-dotnet/commit/1ce965312ea0efe895c66f0025fd68e822ef1d29))

## [2.2.0](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.1.0...v2.2.0) (2026-10-18)


### ⚠ BREAKING CHANGES

* redemptions verify now takes a body. This is the only public break in this release.
  before: `puntjes.Redemptions[code].Verify.PostAsync()`
  after: `puntjes.Redemptions[code].Verify.PostAsync(new VerifyRedemptionData())`
  The body is optional in the API, so an empty `VerifyRedemptionData` sends the same request as before.
  `ToPostRequestInformation()` on the same builder takes the body in the same way.
  The next patch release makes the body optional again, so `PostAsync()` works as before.

### Features

* regenerate from puntjes-openapi v2.2.0 ([#13](https://github.com/PuntjesApp/puntjes-dotnet/issues/13)) ([dd6b184](https://github.com/PuntjesApp/puntjes-dotnet/commit/dd6b184be27bc2c49a757b55835ccba89e7f9fc9))

## [2.1.0](https://github.com/PuntjesApp/puntjes-dotnet/compare/v2.0.0...v2.1.0) (2026-10-15)


### Features

* regenerate from puntjes-openapi v2.1.0 ([#9](https://github.com/PuntjesApp/puntjes-dotnet/issues/9)) ([4aec48c](https://github.com/PuntjesApp/puntjes-dotnet/commit/4aec48c2d145e9ed3368b0324bc658b2ad6b1339))

## [2.0.0](https://github.com/PuntjesApp/puntjes-dotnet/compare/v1.0.0...v2.0.0) (2026-10-12)


### ⚠ BREAKING CHANGES

* regenerate from puntjes-openapi v2.0.0 ([#7](https://github.com/PuntjesApp/puntjes-dotnet/issues/7))

### Features

* regenerate from puntjes-openapi v2.0.0 ([#7](https://github.com/PuntjesApp/puntjes-dotnet/issues/7)) ([bd4b569](https://github.com/PuntjesApp/puntjes-dotnet/commit/bd4b5696740eff99d119347dd9889c1db4002d64))

## 1.0.0 (2026-10-11)


### Features

* Puntjes.Sdk, a Kiota client with a thin layer, regenerate robot and NuGet release ([#1](https://github.com/PuntjesApp/puntjes-dotnet/issues/1)) ([2d8a947](https://github.com/PuntjesApp/puntjes-dotnet/commit/2d8a94784b439b2524649e06564fcc232460a889))
