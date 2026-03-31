using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class ValidationBatchEntryPoint
{
    public static void RunContentValidation()
    {
        var report = ContentDefinitionValidator.ValidateAndWriteReport();
        Debug.Log($"[ValidationBatch] Content validation report written: {report.JsonReportPath}");

        var errorCount = report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error);
        if (errorCount > 0)
        {
            throw new Exception($"Content validation failed with {errorCount} error(s).");
        }
    }

    public static void RunBalanceSweepSmoke()
    {
        var report = BalanceSweepRunner.RunSmokeAndWriteReport();
        Debug.Log($"[ValidationBatch] Balance sweep report written: {report.JsonReportPath}");

        if (report.ValidationErrorCount > 0)
        {
            throw new Exception($"Balance sweep aborted because content validation reported {report.ValidationErrorCount} error(s).");
        }

        if (report.Scenarios.Any(scenario => !scenario.CompileHashDeterministic || !scenario.FinalStateDeterministic))
        {
            throw new Exception("Balance sweep detected a determinism failure. Inspect Logs/balance-sweep artifacts.");
        }
    }
}
