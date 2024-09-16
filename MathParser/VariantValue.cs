
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
	internal class VariantValue
	{
		private readonly VariantType variantType;
		private readonly bool? boolValue;
		private readonly double? doubleValue;
		private readonly string? stringValue;

		public VariantValue(bool value)
		{
			boolValue = value;
			variantType = VariantType.Boolean;
		}
		public VariantValue(double value)
		{
			doubleValue = value;
			variantType = VariantType.Double;
		}
		public VariantValue(int value)
		{
			doubleValue = value;
			variantType = VariantType.Double;
		}
		public VariantValue(string value)
		{
			stringValue = value;
			variantType = VariantType.String;
		}
		public VariantType VariantType => variantType;
		public bool BooleanValue => boolValue ?? false;
		public double DoubleValue => doubleValue ?? 0.0;
		public string StringValue => stringValue ?? string.Empty;
		public int CompareTo(VariantValue other)
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
		private readonly List<VariantValue> list;

		public VariantList()
		{
			list = new List<VariantValue>();
		}
		public void Add(bool value)
		{
			list.Add(new VariantValue(value));
		}
		public void Add(double value)
		{
			list.Add(new VariantValue(value));
		}
		public void Add(double[] values)
		{
			foreach (var v in values)
			{
				Add(v);
			}
		}
		public void Add(VariantValue other)
		{
			list.Add(other);
		}

		public void Add(string value)
		{
			list.Add(new VariantValue(value));
		}

		public int Count
		{
			get => list.Count;
		}

		public double this[int i]
		{
			get => list[i].DoubleValue;
		}

		public bool GetBoolean(int i)
		{
			return list[i].BooleanValue;
		}

		public string GetString(int i)
		{
			return list[i].StringValue;
		}

		public VariantValue ItemAt(int index)
		{
			if (index >= 0 && index < list.Count)
			{
				return list[index];
			}

			throw new CalculatorException("ItemAt index is out of range");
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

		public VariantValue[] ToArray()
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
