
#nullable enable

namespace Mathos.Parser
{
	using System;
	using System.Collections.Generic;

	internal enum VariantType
	{
		Boolean,
		Double,
		String,
		Empty
	}
	internal class Variant
	{
		private readonly VariantType variantType;
		private readonly bool? boolValue;
		private readonly double? doubleValue;
		private readonly string? stringValue;

		public Variant(bool value)
		{
			boolValue = value;
			variantType = VariantType.Boolean;
		}
		public Variant(double value)
		{
			doubleValue = value;
			variantType = VariantType.Double;
		}
		public Variant(int value)
		{
			doubleValue = value;
			variantType = VariantType.Double;
		}
		public Variant(string value)
		{
			stringValue = value;
			variantType = VariantType.String;
		}
		public VariantType VariantType => variantType;
		public bool BooleanValue => boolValue ?? false;
		public double DoubleValue => doubleValue ?? 0.0;
		public string StringValue => stringValue ?? string.Empty;
		public int CompareTo(Variant other)
		{
			if (other.VariantType == VariantType)
			{
				switch (other.VariantType)
				{
					case VariantType.Double:
						return doubleValue is null || other.doubleValue is null ? -1
							: ((double)doubleValue).CompareTo(other.DoubleValue);

					case VariantType.Boolean:
						return boolValue is null || other.boolValue is null ? -1
							: ((bool)boolValue).CompareTo(other.BooleanValue);

					default:
						if (stringValue is null || other.stringValue is null)
						{
							return -1;
						}
						var v1 = stringValue.ToLowerInvariant();
						var v2 = other.StringValue.ToLowerInvariant();
						return v1.CompareTo(v2);
				}
			}

			return -1;
		}

		public override string ToString()
		{
			return variantType switch
			{
				VariantType.Boolean => boolValue == true ? "True" : "False",
				VariantType.Double => doubleValue?.ToString() ?? "0",
				VariantType.String => stringValue ?? string.Empty,
				_ => string.Empty
			};
		}
	}

	internal class VariantList
	{
		private readonly List<Variant> list;

		public VariantList()
		{
			list = new List<Variant>();
		}
		public VariantList(params double[] values)
			: this()
		{
			foreach (var value in values)
			{
				list.Add(new Variant(value));
			}
		}
		public VariantList(IEnumerable<double> values)
			: this()
		{
			foreach (var value in values)
			{
				list.Add(new Variant(value));
			}
		}
		public int Count
		{
			get => list.Count;
		}
		public double this[int i]
		{
			get => list[i].DoubleValue;
		}
		public void Add(Variant variant)
		{
			list.Add(variant);
		}
		public VariantList Assert(params VariantType[] types)
		{
			// list should contain at least the required types
			if (list.Count < types.Length)
			{
				throw new CalculatorException($"expected {types.Length} parameters, only given {list.Count}");
			}

			for (int i = 0; i < Math.Min(types.Length, list.Count); i++)
			{
				if (list[i].VariantType != types[i])
				{
					throw new CalculatorException($"parameter {i} is not of type {types[i]}");
				}
			}

			return this;
		}
		public Variant[] ToArray()
		{
			return list.ToArray();
		}
		public double[] ToDoubleArray()
		{
			var doubles = new List<double>();
			foreach (var value in list)
			{
				if (value.VariantType == VariantType.Double)
				{
					doubles.Add(value.DoubleValue);
				}
				else if (
					value.VariantType == VariantType.String &&
					!string.IsNullOrWhiteSpace(value.StringValue) &&
					double.TryParse(value.StringValue, out double d))
				{
					doubles.Add(d);
				}
			}

			return doubles.ToArray();
		}
	}
}
