{
  "schemaVersion": 1,
  "recordedDateUtc": "2026-09-25",
  "repository": "wieslawsoltes/TreeDataGrid",
  "pullRequest": 26,
  "branch": "codex/uno-core-port",
  "startingHead": "bf703838bcaa16cbc059f3a7a339b528777a050b",
  "testedImplementation": "ab5f30a6d00bfa33332d750b6e7e1ea2c6f1af4f",
  "testedTree": "df3410e3bf492d312abe82d02b9e5dce274cfcb6",
  "ciMergeCommit": "2897938891625862b6b8f3af1829d1c9aae5d171",
  "sourceVerification": {
    "localReviewedTreeEqualsGitHubTree": true,
    "ciArchivedTreeEqualsTestedTree": true,
    "all14ChangedFilesByteComparedWithArchive": true,
    "sourceTarDigestVerified": true,
    "sourceTarSha256": "ae56adb52d5eb293d715a33ccc000e41abdace1f5f71ef049aacb641e9689e2b"
  },
  "authoredCoverage": {
    "newUnoUnitCases": 21,
    "newNativeScenarios": 10,
    "newRegisteredNativeSuites": 0,
    "newBrowserPointerStages": 3,
    "browserInputStagesPerScale": 15,
    "newDirectFrameworkCases": 0
  },
  "functional": {
    "runId": 36169575190,
    "jobId": 108185550222,
    "conclusion": "success",
    "outcomes": {
      "core": 0,
      "uno": 0,
      "avalonia": 0,
      "sample-state": 0,
      "contract-parity": 0,
      "native": 0,
      "native-smoke": 0,
      "activity": 0,
      "activity-smoke": 0,
      "isolated-suites": 0,
      "avalonia-api-build": 0,
      "api-audit": 0,
      "api-self-check": 0,
      "parity-review-tests": 0,
      "parity-review": 0
    },
    "dotnetCases": {
      "uno-tests": {
        "total": 882,
        "passed": 882,
        "failed": 0,
        "notExecuted": 0
      },
      "core-tests": {
        "total": 228,
        "passed": 228,
        "failed": 0,
        "notExecuted": 0
      },
      "avalonia-tests": {
        "total": 536,
        "passed": 536,
        "failed": 0,
        "notExecuted": 0
      },
      "contract-parity-tests": {
        "total": 171,
        "passed": 171,
        "failed": 0,
        "notExecuted": 0
      },
      "sample-state-tests": {
        "total": 41,
        "passed": 41,
        "failed": 0,
        "notExecuted": 0
      }
    },
    "totalDotnetCases": 1858,
    "nativeSuites": {
      "registered": 65,
      "passed": 65
    },
    "newMarkerInIsolatedNativeLog": true,
    "newMarkerInSequentialNativeLog": true,
    "apiAudit": {
      "baselineShapes": 1845,
      "targetShapes": 1847,
      "exactNormalizedMatches": 1011,
      "missingOrDifferent": 834,
      "additionalOrDifferent": 836,
      "unresolvedBaselineTypes": [],
      "unresolvedTargetTypes": [],
      "completeApiParityProven": false
    },
    "strictSelfComparison": "1847/1847 exact, zero differences and no unresolved dependencies",
    "nativeBuildWarnings": 0,
    "nativeBuildErrors": 0,
    "activityBuildWarnings": 0,
    "activityBuildErrors": 0,
    "pythonReviewCasesPassed": 12,
    "metadataSemanticChecksPassed": 39,
    "normalizationChecksPassed": 57
  },
  "platform": {
    "runId": 36169575203,
    "ubuntuJob": 108185548655,
    "windowsJob": 108185548663,
    "macosJob": 108185548831,
    "linuxNativeJob": 108185548859,
    "windowsNativeJob": 108185548752,
    "browserJob": 108185548329,
    "completedJobs": [
      {
        "id": 108185548655,
        "name": "Ubuntu builds and tests",
        "conclusion": "success"
      },
      {
        "id": 108185548663,
        "name": "Windows builds and tests",
        "conclusion": "success"
      },
      {
        "id": 108185548831,
        "name": "macOS builds and tests",
        "conclusion": "success"
      },
      {
        "id": 108185548859,
        "name": "Linux native runtime and NuGet consumers",
        "conclusion": "success"
      },
      {
        "id": 108185548752,
        "name": "Windows App SDK builds and package publication",
        "conclusion": "success"
      },
      {
        "id": 108185548329,
        "name": "Published trimmed Chromium samples and input",
        "conclusion": "success"
      }
    ],
    "conclusion": "success",
    "status": "completed"
  },
  "performance": {
    "runId": 36169575234,
    "conclusion": "failure",
    "beforeAfterExperimentForThisChange": false,
    "comparisons": [
      {
        "operation": "distant-diagonal-scroll",
        "avaloniaMs": 2.7603,
        "unoMs": 9.3,
        "timeRatio": 3.3691990001086842,
        "allocationRatio": 1.9284410078910181
      },
      {
        "operation": "replace-visible-row",
        "avaloniaMs": 2.4563,
        "unoMs": 3.7796,
        "timeRatio": 1.5387371249440214,
        "allocationRatio": 2.6677908937605395
      },
      {
        "operation": "resize-visible-column",
        "avaloniaMs": 4.8451,
        "unoMs": 6.53815,
        "timeRatio": 1.3494355121669315,
        "allocationRatio": 1.5160611329733686
      },
      {
        "operation": "scroll-x",
        "avaloniaMs": 0.5095000000000001,
        "unoMs": 2.6658,
        "timeRatio": 5.232188420019626,
        "allocationRatio": 1.0634441087613293
      },
      {
        "operation": "scroll-y",
        "avaloniaMs": 1.0605,
        "unoMs": 2.52575,
        "timeRatio": 2.3816595945308814,
        "allocationRatio": 1.9566068515497552
      },
      {
        "operation": "sort",
        "avaloniaMs": 44.8647,
        "unoMs": 65.3247,
        "timeRatio": 1.4560378203799424,
        "allocationRatio": 0.6197229488476887
      }
    ],
    "budget": 1.1,
    "budgetMet": false,
    "scope": "Synchronous UI-thread source/layout work and verified layout-settlement latency. Not GPU completion, frame rate, input latency, accessibility, variable-height or all-feature parity.",
    "pairs": 2
  },
  "artifacts": [
    {
      "id": 10879843127,
      "name": "uno-validation-report",
      "bytes": 4667861,
      "sha256": "a7efa0598a66ea54caf5b39463377176cf44621593372f2851c62770ceae6457",
      "independentlyRecomputed": true
    },
    {
      "id": 10879827301,
      "name": "uno-validation-source",
      "bytes": 1537362,
      "sha256": "fab1d6b4ecd30f366664c16cadaf01a115d1bd6b93ba0b04d56d5e8b032d3d97",
      "independentlyRecomputed": true
    },
    {
      "id": 10879298204,
      "name": "uno-tests-ubuntu-latest",
      "bytes": 274479,
      "sha256": "3d5bda0af2ac1cb2a370b73b54da43dc355dd3d40cdca9e3d6333e0f5011a75f",
      "independentlyRecomputed": true
    },
    {
      "id": 10880147526,
      "name": "native-parity",
      "bytes": 18238,
      "sha256": "b9813958ae0f894055f0c61a42a46fa6bbfe736b3dc94fb14a7327c084863cca",
      "independentlyRecomputed": true
    },
    {
      "id": 10879204744,
      "name": "uno-browser-samples",
      "bytes": 181900647,
      "sha256": "d1806180723236b13c2d89e615036c2c2beb77afe0657ef5c27f98fe2bcdafbe",
      "independentlyRecomputed": true
    }
  ],
  "localChecks": {
    "pythonDriverSyntax": "passed",
    "all15DriverStagesWithFakePage": "passed",
    "invalidCoordinatesRejected": "NaN, infinity, negative, past-viewport",
    "outOfOrderReadinessRejected": true,
    "dotnetExecutedLocally": false,
    "csharpProducerMatchesPython15StageOrder": true
  },
  "boundaries": {
    "fullApiParityProven": false,
    "fullPerformanceParityProven": false,
    "originalAllocationFailureDiagnosed": false,
    "preexistingUnitAssertionsChanged": false,
    "coreImplementationChanged": false,
    "rendererOrBudgetChanged": false,
    "merged": false,
    "publicReleaseCreated": false,
    "draft": true,
    "retriesRequestedForThisImplementation": 0,
    "windowsNativeBuildIsNotWindowsOSRuntimeAcceptance": true,
    "oldWarmLayoutAllocationCasesPassedUnchangedOnUbuntu": [
      1,
      128,
      129,
      1024
    ]
  },
  "documentationOnlyFollowup": true,
  "supportingWorkflows": [
    {
      "id": 36169575285,
      "name": "Build",
      "conclusion": "success"
    },
    {
      "id": 36169575288,
      "name": "Uno trimmed binding contract",
      "conclusion": "success"
    },
    {
      "id": 36169575175,
      "name": "Uno dependency snapshot",
      "conclusion": "success"
    },
    {
      "id": 36169575266,
      "name": "Uno contract reproducibility",
      "conclusion": "success"
    },
    {
      "id": 36169575185,
      "name": "Uno reference packs",
      "conclusion": "success"
    }
  ],
  "browser": {
    "runId": 36169575203,
    "jobId": 108185548329,
    "conclusion": "success",
    "revision": "2897938891625862b6b8f3af1829d1c9aae5d171",
    "chromiumVersion": "143.0.7499.4",
    "passedRoutes": [
      "showcase",
      "monitor",
      "showcase-input-scale-1",
      "showcase-input-scale-2"
    ],
    "failedRoutes": [],
    "inputStagesPerScale": 15,
    "deviceScaleFactors": [
      1,
      2
    ],
    "inputStages": [
      "select-row",
      "arrow-down",
      "begin-edit",
      "commit-edit",
      "select-cancel-row",
      "begin-cancel-edit",
      "cancel-edit",
      "ctrl-select",
      "resize-column",
      "cancel-resize",
      "sort-column",
      "sort-column-descending",
      "sort-column-clear",
      "sort-column-restart",
      "wheel-scroll"
    ],
    "newMarkerObservedAtBothScales": true,
    "showcaseNativeScenariosPassed": 10,
    "showcaseNativeMarkerObserved": true,
    "consoleAndSummaryExtractedAndInspected": true,
    "scope": "Published trimmed consumers, Chromium assertions and browser-dispatched pointer/keyboard input at device scale factors 1 and 2. Not physical hardware, all-browser, IME or external screen-reader acceptance.",
    "completeBrowserParityProven": false
  }
}
