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

    [Fact]
    public void BuildBudgetAllocationQuery_MatchesLiveGgmsSchema()
    {
        // Act
        var query = GgmsBudgetSyncService.BuildBudgetAllocationQuery(null, null);

        // Assert — budget_allocations joins tbl_offices by numeric office_id, filtered by office_code
        Assert.Contains("FROM `budget_allocations`", query, StringComparison.Ordinal);
        Assert.Contains("alloc.`master_budget_id`", query, StringComparison.Ordinal);
        Assert.Contains("alloc.`amount`", query, StringComparison.Ordinal);
        Assert.Contains("alloc.`used_amount`", query, StringComparison.Ordinal);
        Assert.Contains("ON office.`id` = alloc.`office_id`", query, StringComparison.Ordinal);
        Assert.Contains("WHERE office.`office_code` = @officeCode", query, StringComparison.Ordinal);
        Assert.Contains("ORDER BY alloc.`master_budget_id` DESC, alloc.`id` DESC", query, StringComparison.Ordinal);
        // Legacy column names must not leak into the new-table query:
        Assert.DoesNotContain("YearlyBudgetId", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AllocatedAmount", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildLegacyAllocationQuery_KeepsOfficeallocationsShape()
    {
        // Act
        var query = GgmsBudgetSyncService.BuildLegacyAllocationQuery(null, null);

        // Assert — fallback keeps the original officeallocations contract
        Assert.Contains("FROM `officeallocations`", query, StringComparison.Ordinal);
        Assert.Contains("alloc.`YearlyBudgetId`", query, StringComparison.Ordinal);
        Assert.Contains("alloc.`AllocatedAmount`", query, StringComparison.Ordinal);
        Assert.Contains("ON office.`office_code` = alloc.`office_code`", query, StringComparison.Ordinal);
        Assert.Contains("WHERE alloc.`office_code` = @officeCode", query, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAllocationQueries_UseConfiguredTableNames_WhenProvided()
    {
        var newQuery = GgmsBudgetSyncService.BuildBudgetAllocationQuery(" custom_alloc ", " custom_offices ");
        Assert.Contains("`custom_alloc`", newQuery, StringComparison.Ordinal);
        Assert.Contains("`custom_offices`", newQuery, StringComparison.Ordinal);

        var legacyQuery = GgmsBudgetSyncService.BuildLegacyAllocationQuery(" custom_legacy ", " custom_offices ");
        Assert.Contains("`custom_legacy`", legacyQuery, StringComparison.Ordinal);
        Assert.Contains("`custom_offices`", legacyQuery, StringComparison.Ordinal);
    }
}
