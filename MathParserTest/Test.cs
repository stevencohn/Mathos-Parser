using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Mathos.Parser.Test
{
	[TestClass]
	public class Test
	{
		[TestMethod]
		public void BasicArithmetic()
		{
			var calculator = new Calculator();

			Assert.AreEqual(7, calculator.Compute("5 + 2"));
			Assert.AreEqual(11, calculator.Compute("5 + 2 * 3"));
			Assert.AreEqual(17, calculator.Compute("27 - 3 * 3 + 1 - 4 / 2"));
			Assert.AreEqual(282429536481, calculator.Compute("(27 ^ 2) ^ 4"));
		}

		[TestMethod]
		public void Tables()
		{
			var calculator = new Calculator();

			calculator.SetVariable("tablecols", 5);
			calculator.SetVariable("tablerows", 10);

			calculator.GetCellValue += (object sender, GetCellValueEventArgs args) =>
			{
				args.Value = 123.ToString();
				Debug.WriteLine($"GetCellValue({args.Name}) => [{args.Value}]");
			};

			Assert.AreEqual(123, calculator.Compute("a1"));
			Assert.AreEqual(125, calculator.Compute("A1 + 2"));

			Assert.AreEqual(7, calculator.Compute("tablecols + 2"));

			// current cell at bottom of A col
			calculator.SetVariable("col", 1);
			calculator.SetVariable("row", 10);
			// sum first three rows
			Assert.AreEqual(123 * 3, calculator.Compute("sum(a1:a3)"));
			// sum A1:A9 - all rows except last row (current cell)
			Assert.AreEqual(123 * 9, calculator.Compute("sum(A1:cell(0,-1))"));

			// current cell at A4
			calculator.SetVariable("col", 1);
			calculator.SetVariable("row", 4);
			// sum A1:A3
			Assert.AreEqual(123 * 3, calculator.Compute("sum(A1:cell(0,-1))"));

			// current cell at B1
			calculator.SetVariable("col", 2);
			calculator.SetVariable("row", 1);
			// sum B2:B3
			Assert.AreEqual(123 * 2, calculator.Compute("sum(B2:cell(0,2))"));
			// sum B2:B9
			Assert.AreEqual(123 * 9, calculator.Compute("sum(B2:cell(0,tablerows-1))"));
			// sum C1:E1
			Assert.AreEqual(123 * 4, calculator.Compute("sum(C1:cell(tablecols - 1, 0))"));
		}


		[TestMethod]
		public void Tables_CountIf()
		{
			var calculator = new Calculator();

			calculator.SetVariable("tablecols", 5);
			calculator.SetVariable("tablerows", 10);

			calculator.GetCellValue += (object sender, GetCellValueEventArgs args) =>
			{
				var col = args.Name[0];
				var row = int.Parse(args.Name.Substring(1));

				if (col == 'A')
				{
					args.Value = row.ToString();
				}
				else if (col == 'B')
				{
					args.Value = (row % 3).ToString();
				}
				else if (col == 'C')
				{
					args.Value = row < 5 ? "abc" : "xyz";
				}
				else
				{
					args.Value = row % 2 == 1 ? "True" : "False";
				}

				Debug.WriteLine($"GetCellValue({args.Name}) => [{args.Value}]");
			};

			Assert.AreEqual(1, calculator.Compute("countif(A1:A3, 1 + 1)"));
			Assert.AreEqual(4, calculator.Compute("countif(B1:B10, 1)"));
			Assert.AreEqual(6, calculator.Compute("countif(B1:B10, !1)"));
			Assert.AreEqual(4, calculator.Compute("countif(A1:A10, < A5)"));
			Assert.AreEqual(5, calculator.Compute("countif(D1:D10, true)"));
			Assert.AreEqual(6, calculator.Compute("countif(C1:C10, !abc)"));

			calculator.SetVariable("col", 3);
			calculator.SetVariable("row", 10);
			Assert.AreEqual(6, calculator.Compute("countif(C1:cell(0,0), !abc)"));
		}


		[TestMethod]
		public void AdvancedArithmetic()
		{
			var calculator = new Calculator();

			Assert.AreEqual(30, calculator.Compute("3(7+3)"));
			Assert.AreEqual(20, calculator.Compute("(2+3)(3+1)"));
		}

		[TestMethod]
		public void DivideByZero()
		{
			var calculator = new Calculator();

			Assert.AreEqual(double.PositiveInfinity, calculator.Compute("5 / 0"));
			Assert.AreEqual(double.NegativeInfinity, calculator.Compute("(-30) / 0"));
			Assert.AreEqual(double.NaN, calculator.Compute("0 / 0"));

			//Assert.AreEqual(double.PositiveInfinity, calculator.Parse("5 : 0"));
			//Assert.AreEqual(double.NegativeInfinity, calculator.Parse("(-30) : 0"));
			//Assert.AreEqual(double.NaN, calculator.Parse("0 : 0"));
		}

		[TestMethod]
		public void ConditionalStatements()
		{
			var calculator = new Calculator();

			Assert.AreEqual(1, calculator.Compute("2 + 3 = 1 + 4"));
			Assert.AreEqual(1, calculator.Compute("3 + 2 > 2 - 1"));
			Assert.AreEqual(1, calculator.Compute("(2+3)(3+1) < 50 - 20"));

			Assert.AreEqual(0, calculator.Compute("2 + 2 = 22"));
			Assert.AreEqual(0, calculator.Compute("(2+3)(3+1) > 50 - 20"));
			Assert.AreEqual(0, calculator.Compute("100 < 10"));

			Assert.AreEqual(1, calculator.Compute("2.5 <= 3"));
			Assert.AreEqual(1, calculator.Compute("(2+3)(3+1) <= 50 - 20"));

			Assert.AreEqual(0, calculator.Compute("100 <= 10"));
			Assert.AreEqual(0, calculator.Compute("(2+3)(3+1) >= 50 - 20"));
		}

		[TestMethod]
		public void ProgramicallyAddVariables()
		{
			var calculator = new Calculator();

			calculator.ProgrammaticallyParse("let a = 2pi");
			Assert.AreEqual(calculator.GetVariable("pi") * 2, calculator.Compute("a"), 0.00000000000001);

			calculator.ProgrammaticallyParse("b := 20");
			Assert.AreEqual(20, calculator.Compute("b"));

			calculator.ProgrammaticallyParse("let c be 25 + 2(2+3)");
			Assert.AreEqual(35, calculator.Compute("c"));

			calculator.VariableDeclarator = "dim";
			calculator.ProgrammaticallyParse("dim d = 5 ^3");
			Assert.AreEqual(125, calculator.Compute("d"));
		}

		[TestMethod]
		public void CustomFunctions()
		{
			var calculator = new Calculator();

			calculator.AddFunction("timesTwo", inputs => inputs[0] * 2);
			Assert.AreEqual(6, calculator.Compute("timesTwo(3)"));
			Assert.AreEqual(42, calculator.Compute("timesTwo((2+3)(3+1) + 1)"));

			calculator.AddFunction("square", inputs => inputs[0] * inputs[0]);
			Assert.AreEqual(16, calculator.Compute("square(4)"));

			calculator.AddFunction("cube", inputs => inputs[0] * inputs[0] * inputs[0]);
			Assert.AreEqual(8, calculator.Compute("cube(2)"));

			calculator.AddFunction("constF", inputs => 12);
			Assert.AreEqual(12, calculator.Compute("constF()"));
			Assert.AreEqual(144, calculator.Compute("constF() * constF()"));

			calculator.AddFunction("argCount", inputs => inputs.Count);
			Assert.AreEqual(0, calculator.Compute("argCount()"));
			Assert.AreEqual(1, calculator.Compute("argCount(1)"));
			Assert.AreEqual(2, calculator.Compute("argCount(argCount(1), -5)"));
			Assert.AreEqual(2, calculator.Compute("argCount(argCount(1, 0), argCount())"));
		}

		//[TestMethod]
		//public void CustomFunctionsWithSeveralArguments()
		//{
		//	var calculator = new MathParser(false);

		//	calculator.LocalFunctions.Add("log", delegate (double[] input)
		//	{
		//		switch (input.Length)
		//		{
		//			case 1:
		//				return Math.Log10(input[0]);
		//			case 2:
		//				return Math.Log(input[0], input[1]);
		//			default:
		//				return 0;
		//		}
		//	});

		//	Assert.AreEqual(0.301029996, calculator.Parse("log(2)"), 0.000000001);
		//	Assert.AreEqual(0.630929754, calculator.Parse("log(2,3)"), 0.000000001);
		//}

		[TestMethod]
		[ExpectedException(typeof(CalculatorException))]
		public void UndefinedVariableException()
		{
			var calculator = new Calculator();

			try
			{
				calculator.ProgrammaticallyParse("unknownvar * 5");
			}
			catch (Exception e)
			{
				// Tests to see if the message the exception gives is clear enough
				Assert.IsTrue(e.Message.ToLowerInvariant().Contains("variable") && e.Message.ToLowerInvariant().Contains("unknownvar"));
				throw e;
			}
		}

		[TestMethod]
		[ExpectedException(typeof(CalculatorException))]
		public void UndefinedOperatorException()
		{
			var calculator = new Calculator();

			try
			{
				calculator.ProgrammaticallyParse("unknownoperator(5)");
			}
			catch (Exception e)
			{
				// Tests to see if the message the exception gives is clear enough
				Assert.IsTrue(e.Message.ToLowerInvariant().Contains("operator") && e.Message.ToLowerInvariant().Contains("unknownoperator"));
				throw e;
			}
		}

		[TestMethod]
		public void NegativeNumbers()
		{
			var calculator = new Calculator();

			Assert.AreEqual(0, calculator.Compute("-1+1"));
			Assert.AreEqual(1, calculator.Compute("--1"));
			Assert.AreEqual(-2, calculator.Compute("-2"));
			Assert.AreEqual(-2, calculator.Compute("(-2)"));
			// Assert.AreEqual(2, calculator.Parse("-(-2)")); TODO: Fix
			Assert.AreEqual(4, calculator.Compute("(-2)(-2)"));
			Assert.AreEqual(-3, calculator.Compute("-(3+2+1+6)/4"));

			calculator.SetVariable("x", 50);

			Assert.AreEqual(-100, calculator.Compute("-x - x"));
			Assert.AreEqual(-75, calculator.Compute("-x * 1.5"));
		}

		[TestMethod]
		public void Trigonometry()
		{
			var calculator = new Calculator();

			Assert.AreEqual(Math.Cos(32) + 3, calculator.Compute("cos(32) + 3"));
		}

		[TestMethod]
		public void CustomizeOperators()
		{
			var calculator = new Calculator();

			calculator.AddOperator("$", (a, b) => a * 2 + b * 3);

			Assert.AreEqual(3 * 2 + 3 * 2, calculator.Compute("3 $ 2"));
		}

		[TestMethod]
		public void DoubleOperations()
		{
			var parserDefault = new Calculator();

			Assert.AreEqual(
				double.Parse("0.055", CultureInfo.InvariantCulture),
				parserDefault.Compute("-0.245 + 0.3"));
		}

		[TestMethod]
		public void ExecutionTime()
		{
			var timer = new Stopwatch();
			var calculator = new Calculator();

			calculator.Compute("5+2*3*1+2((1-2)(2-3))*-1"); // Warm-up

			GC.Collect();
			GC.WaitForPendingFinalizers();

			timer.Start();

			calculator.Compute("5+2");
			calculator.Compute("5+2*3*1+2((1-2)(2-3))");
			calculator.Compute("5+2*3*1+2((1-2)(2-3))*-1");

			timer.Stop();

			Debug.WriteLine("Parse Time: " + timer.Elapsed.TotalMilliseconds + "ms");
		}

		[TestMethod]
		public void BuiltInFunctions()
		{
			var calculator = new Calculator();

			Assert.AreEqual(21, calculator.Compute("round(21.333333333333)"));
			Assert.AreEqual(1, calculator.Compute("pow(2,0)"));
		}

		[TestMethod]
		[ExpectedException(typeof(ArithmeticException))]
		public void ExceptionCatching()
		{
			var calculator = new Calculator();

			calculator.Compute("(-1");
			calculator.Compute("rem(20,1,,,,)");
		}

		[TestMethod]
		public void StrangeStuff()
		{
			var calculator = new Calculator();

			calculator.AddOperator("times", (x, y) => x * y);
			calculator.AddOperator("dividedby", (x, y) => x / y);
			calculator.AddOperator("plus", (x, y) => x + y);
			calculator.AddOperator("minus", (x, y) => x - y);

			Debug.WriteLine(calculator.Compute(
				"5 plus 3 dividedby 2 times 3").ToString(CultureInfo.InvariantCulture));
		}

		[TestMethod]
		public void TestLongExpression()
		{
			var calculator = new Calculator();

			Assert.AreEqual(2, calculator.Compute("4^2-2*3^2+4"));
		}

		//[TestMethod]
		//public void SpeedTests()
		//{
		//	var calculator = new MathParser();

		//	calculator.SetVariable("x", 10);

		//	var list = calculator.GetTokens("(3x+2)");
		//	var time = BenchmarkUtil.Benchmark(() => calculator.Parse("(3x+2)"), 25000);
		//	var time2 = BenchmarkUtil.Benchmark(() => calculator.Parse(list), 25000);

		//	Assert.IsTrue(time >= time2);
		//}

		//[TestMethod]
		//public void DetailedSpeedTestWithOptimization()
		//{
		//	var calculator = new MathParser();

		//	calculator.SetVariable("x", 5);

		//	var expr = "(3x+2)(2(2x+1))";

		//	const int itr = 3000;
		//	var creationTimeAndTokenization = BenchmarkUtil.Benchmark(() => calculator.GetTokens(expr), 1);
		//	var tokens = calculator.GetTokens(expr);

		//	var parsingTime = BenchmarkUtil.Benchmark(() => calculator.Parse(tokens), itr);
		//	var totalTime = creationTimeAndTokenization + parsingTime;

		//	Console.WriteLine("Parsing Time: " + parsingTime);
		//	Console.WriteLine("Total Time: " + totalTime);

		//	var parsingTime2 = BenchmarkUtil.Benchmark(() => calculator.Parse(expr), itr);

		//	Console.WriteLine("Parsing Time 2: " + parsingTime2);
		//	Console.WriteLine("Total Time: " + parsingTime2);
		//}

		[TestMethod]
		public void DetailedSpeedTestWithoutOptimization()
		{
			var calculator = new Calculator();

			calculator.SetVariable("x", 5);

			var expr = "(3x+2)(2(2x+1))";
			const int itr = 50;

			var parsingTime = BenchmarkUtil.Benchmark(() => calculator.Compute(expr), itr);

			Console.WriteLine("Parsing Time: " + parsingTime);
			Console.WriteLine("Total Time: " + parsingTime);
		}

		[TestMethod]
		public void CommaPiBug()
		{
			var calculator = new Calculator();
			var result = calculator.Compute("pi");

			Assert.AreEqual(result, calculator.GetVariable("pi"), 0.00000000000001);
		}

		[TestMethod]
		public void NumberNotations()
		{
			var calculator = new Calculator();

			Assert.AreEqual(0.0005, calculator.Compute("5 * 10^-4"));
		}

		[TestMethod]
		public void NoLeadingZero()
		{
			var calculator = new Calculator();

			Assert.AreEqual(0.5, calculator.Compute(".5"));
			Assert.AreEqual(0.5, calculator.Compute(".25 + .25"));
			Assert.AreEqual(2.0, calculator.Compute("1.5 + .5"));
			Assert.AreEqual(-0.25, calculator.Compute(".25 + (-.5)"));
			Assert.AreEqual(0.25, calculator.Compute(".5(.5)"));
		}

		public class BenchmarkUtil
		{
			public static double Benchmark(Action action, int iterations)
			{
				double time = 0;
				const int innerCount = 5;

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				for (var i = 0; i < innerCount; i++)
					action.Invoke();

				var watch = Stopwatch.StartNew();

				for (var i = 0; i < iterations; i++)
				{
					action.Invoke();

					time += Convert.ToDouble(watch.ElapsedMilliseconds) / Convert.ToDouble(iterations);
				}

				return time;
			}
		}
	}
}
