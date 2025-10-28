using Plugin.Maui.OfflineData.Core;

namespace Plugin.Maui.OfflineData.Tests;

/// <summary>
/// Tests for RecordSet<T> fluent LINQ-style API.
/// </summary>
public class RecordSetTests
{
	[Fact]
	public void RecordSet_Where_ShouldFilter()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.Where(x => x > 3).ToList();

		// Assert
		Assert.Equal(2, results.Count);
		Assert.Contains(4, results);
		Assert.Contains(5, results);
	}

	[Fact]
	public void RecordSet_Select_ShouldProject()
	{
		// Arrange
		var data = new[] { 1, 2, 3 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.Select(x => x * 2).ToList();

		// Assert
		Assert.Equal(new[] { 2, 4, 6 }, results);
	}

	[Fact]
	public void RecordSet_OrderBy_ShouldSort()
	{
		// Arrange
		var data = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.OrderBy(x => x).ToArray();

		// Assert
		Assert.Equal(new[] { 1, 1, 2, 3, 4, 5, 6, 9 }, results);
	}

	[Fact]
	public void RecordSet_OrderByDescending_ShouldSortDescending()
	{
		// Arrange
		var data = new[] { 3, 1, 4 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.OrderByDescending(x => x).ToList();

		// Assert
		Assert.Equal(new[] { 4, 3, 1 }, results);
	}

	[Fact]
	public void RecordSet_Skip_ShouldSkipItems()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.Skip(2).ToList();

		// Assert
		Assert.Equal(new[] { 3, 4, 5 }, results);
	}

	[Fact]
	public void RecordSet_Take_ShouldTakeItems()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var results = recordSet.Take(3).ToList();

		// Assert
		Assert.Equal(new[] { 1, 2, 3 }, results);
	}

	[Fact]
	public void RecordSet_First_ShouldReturnFirst()
	{
		// Arrange
		var data = new[] { 1, 2, 3 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var result = recordSet.First();

		// Assert
		Assert.Equal(1, result);
	}

	[Fact]
	public void RecordSet_FirstOrDefault_WithEmptySet_ShouldReturnDefault()
	{
		// Arrange
		var data = Array.Empty<int>();
		var recordSet = new RecordSet<int>(data);

		// Act
		var result = recordSet.FirstOrDefault();

		// Assert
		Assert.Equal(0, result);
	}

	[Fact]
	public void RecordSet_Single_WithOneItem_ShouldReturnItem()
	{
		// Arrange
		var data = new[] { 42 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var result = recordSet.Single();

		// Assert
		Assert.Equal(42, result);
	}

	[Fact]
	public void RecordSet_Any_WithPredicate_ShouldCheckCondition()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act & Assert
		Assert.True(recordSet.Any(x => x > 4));
		Assert.False(recordSet.Any(x => x > 10));
	}

	[Fact]
	public void RecordSet_Any_WithoutPredicate_ShouldCheckIfNotEmpty()
	{
		// Arrange
		var emptySet = new RecordSet<int>(Array.Empty<int>());
		var nonEmptySet = new RecordSet<int>(new[] { 1 });

		// Act & Assert
		Assert.False(emptySet.Any());
		Assert.True(nonEmptySet.Any());
	}

	[Fact]
	public void RecordSet_All_ShouldCheckAllItems()
	{
		// Arrange
		var data = new[] { 2, 4, 6, 8 };
		var recordSet = new RecordSet<int>(data);

		// Act & Assert
		Assert.True(recordSet.All(x => x % 2 == 0));
		Assert.False(recordSet.All(x => x > 5));
	}

	[Fact]
	public void RecordSet_Count_ShouldReturnCount()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var count = recordSet.Count();

		// Assert
		Assert.Equal(5, count);
	}

	[Fact]
	public void RecordSet_Count_WithPredicate_ShouldReturnMatchingCount()
	{
		// Arrange
		var data = new[] { 1, 2, 3, 4, 5 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var count = recordSet.Count(x => x > 3);

		// Assert
		Assert.Equal(2, count);
	}

	[Fact]
	public void RecordSet_FluentChaining_ShouldWorkCorrectly()
	{
		// Arrange
		var data = new[] { 
			new TestPerson { Name = "Alice", Age = 30 },
			new TestPerson { Name = "Bob", Age = 25 },
			new TestPerson { Name = "Charlie", Age = 35 },
			new TestPerson { Name = "David", Age = 28 },
			new TestPerson { Name = "Eve", Age = 32 }
		};
		var recordSet = new RecordSet<TestPerson>(data);

		// Act - Complex fluent query
		var results = recordSet
			.Where(p => p.Age >= 28)
			.OrderBy(p => p.Age)
			.Select(p => p.Name)
			.Take(3)
			.ToList();

		// Assert
		Assert.Equal(3, results.Count);
		Assert.Equal(new[] { "David", "Alice", "Eve" }, results);
	}

	[Fact]
	public void RecordSet_AsEnumerable_ShouldReturnUnderlyingEnumerable()
	{
		// Arrange
		var data = new[] { 1, 2, 3 };
		var recordSet = new RecordSet<int>(data);

		// Act
		var enumerable = recordSet.AsEnumerable();

		// Assert
		Assert.NotNull(enumerable);
		Assert.Equal(3, enumerable.Count());
	}

	private class TestPerson
	{
		public string Name { get; set; } = string.Empty;
		public int Age { get; set; }
	}
}
