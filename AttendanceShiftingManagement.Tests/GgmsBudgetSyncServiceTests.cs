using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class GgmsBudgetSyncServiceTests
{
    [Fact]
    public void BuildSpentAmountQuery_UsesSumOfAmount_WithReleasedStatus()
    {
        // Act
        var query = GgmsBudgetSyncService.BuildSpentAmountQuery(null);

        // Assert
        Assert.Contains("SUM(amount)", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status = 'Released'", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("office_id = @officeCode", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consolidated_transactions", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSpentAmountQuery_UsesConfiguredTableName_WhenProvided()
    {
        // Act
        var query = GgmsBudgetSyncService.BuildSpentAmountQuery(" custom_transactions_table ");

        // Assert
        Assert.Contains("custom_transactions_table", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consolidated_transactions", query, StringComparison.OrdinalIgnoreCase);
    }
}
