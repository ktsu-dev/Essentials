// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class KeyedHashProviderTests
{
	#region FixedTimeComparison

	[TestMethod]
	public void FixedTimeComparison_Matches_Identical_Spans()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 4];

		Assert.IsTrue(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Rejects_Single_Bit_Difference()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 5];

		Assert.IsFalse(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Rejects_Different_Lengths()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3];

		Assert.IsFalse(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Matches_Empty_Spans()
	{
		Assert.IsTrue(FixedTimeComparison.Equals([], []));
	}

	#endregion
}
