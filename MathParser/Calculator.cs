﻿//************************************************************************************************
// Copyright © 2024 Steven M Cohn. All rights reserved.
//
// This is a heavily modified adaptation of the Mathos.Parser by artem@atemlos.net, customized
// for the OneMore table calculator. Copyright © 2012-2019, Mathos Project. All rights reserved.
//
//************************************************************************************************

#pragma warning disable S1643 // Strings should not be concatenated using '+' in a loop

namespace Mathos.Parser
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;


	/// <summary>
	/// A mathematical expression parser and evaluator with adaptations for spreadsheet-like
	/// cell dereferencing and enhanced functions.
	/// </summary>
	internal class Calculator
	{
		// Regex pattern for matching cell addresses of the form [col-letters][row-number] where
		// row-number is a positive, non-zero integer. Capture groups are named c)ell and r)row.
		private const string AddressPattern = @"^(?<c>[a-zA-Z]+)(?<r>\d+)$";

		// names of specialized functions
		private const string CellFnName = "cell";
		private const string CountifFnName = "countif";

		// single char unicode values for greater-than, less-than, and not-equals-to
		private const char GeqSign = (char)8805;
		private const char LeqSign = (char)8804;
		private const char NeqSign = (char)8800;

		private readonly FunctionFactory factory;

		private readonly Dictionary<string, Func<double, double, double>> operators;
		private readonly Dictionary<string, double> variables;
		private readonly Dictionary<string, Func<VariantList, double>> functions;
		private readonly CultureInfo cultureInfo;


		/// <summary>
		/// The keyword to use for variable declarations when parsing. The default value is "let".
		/// </summary>
		public string VariableDeclarator { get; set; } = "let";


		/// <summary>
		/// The handler used as a callback to a consumer delegate to ask for cell values.
		/// </summary>
		public event GetCellValueHandler GetCellValue;



		/// <summary>
		/// Iniitalizes a new calculator.
		/// </summary>
		public Calculator()
		{
			operators = new Dictionary<string, Func<double, double, double>>
			{
				["^"] = Math.Pow,
				["%"] = (a, b) => a % b,
				["/"] = (a, b) =>
				{
					if (b != 0)
						return a / b;
					else if (a > 0)
						return double.PositiveInfinity;
					else if (a < 0)
						return double.NegativeInfinity;
					else
						return double.NaN;
				},
				["*"] = (a, b) => a * b,
				["-"] = (a, b) => a - b,
				["+"] = (a, b) => a + b,

				[">"] = (a, b) => a > b ? 1 : 0,
				["<"] = (a, b) => a < b ? 1 : 0,
				["" + GeqSign] = (a, b) => a > b || Math.Abs(a - b) < 0.00000001 ? 1 : 0,
				["" + LeqSign] = (a, b) => a < b || Math.Abs(a - b) < 0.00000001 ? 1 : 0,
				["" + NeqSign] = (a, b) => Math.Abs(a - b) < 0.00000001 ? 0 : 1,
				["="] = (a, b) => Math.Abs(a - b) < 0.00000001 ? 1 : 0
			};

			variables = new Dictionary<string, double>
			{
				["pi"] = 3.14159265358979,
				["tao"] = 6.28318530717959,

				["e"] = 2.71828182845905,
				["phi"] = 1.61803398874989,
				["major"] = 0.61803398874989,
				["minor"] = 0.38196601125011,

				["pitograd"] = 57.2957795130823,
				["piofgrad"] = 0.01745329251994
			};

			functions = new Dictionary<string, Func<VariantList, double>>();
			factory = new FunctionFactory();

			cultureInfo = CultureInfo.InvariantCulture;
		}


		/// <summary>
		/// Adds or replaces a named user-defined function.
		/// </summary>
		/// <param name="name">The unique name of the function</param>
		/// <param name="fn">The function</param>
		public void AddFunction(string name, Func<VariantList, double> fn)
		{
			if (!functions.ContainsKey(name))
			{
				functions.Add(name, fn);
			}
			else
			{
				functions[name] = fn;
			}
		}


		/// <summary>
		/// Adds or replaces a named user-defined operator.
		/// </summary>
		/// <param name="name">The unique operator syntax; this should be a single char.</param>
		/// <param name="fn">The operator evaluation function</param>
		public void AddOperator(string name, Func<double, double, double> fn)
		{
			if (!operators.ContainsKey(name))
			{
				operators.Add(name, fn);
			}
			else
			{
				operators[name] = fn;
			}
		}


		/// <summary>
		/// Gets the value of a named variable.
		/// </summary>
		/// <param name="name">The name of the variable.</param>
		/// <returns>The value of the variable</returns>
		public double GetVariable(string name)
		{
			if (variables.ContainsKey(name))
			{
				return variables[name];
			}

			return double.NaN;
		}


		/// <summary>
		/// Adds or replaces a named user-defined variable.
		/// </summary>
		/// <param name="name">The unique name of the variable.</param>
		/// <param name="value">The value of the variable</param>
		public void SetVariable(string name, double value)
		{
			if (!variables.ContainsKey(name))
			{
				variables.Add(name, value);
			}
			else
			{
				variables[name] = value;
			}
		}


		/// <summary>
		/// Parse and evaluate a mathematical expression.
		/// </summary>
		/// <remarks>
		/// This method does not evaluate variable declarations.
		/// </remarks>
		/// <param name="expression">The math expression to parse and evaluate.</param>
		/// <returns>Returns the result of executing the given math expression.</returns>
		public double Compute(string expression)
		{
			var tokens = Parse(expression);
			ReplaceVariables(tokens);
			PreprocessTableFunctions(tokens);
			GetCellContents(tokens);
			return Evaluate(tokens);
		}


		#region ProgrammaticallyParse
		/// <summary>
		/// Parse and evaluate a mathematical expression with comments and variable declarations 
		/// taken into account.
		/// </summary>
		/// <remarks>
		/// The syntax for declaring/editing a variable is either "let a = 0", "let a be 0", 
		/// or "let a := 0" where
		/// "let" is the keyword specified by <see cref="VariableDeclarator"/>.
		/// 
		/// This method evaluates comments and variable declarations.
		/// </remarks>
		/// <example>
		/// <code>
		/// using System.Diagnostics;
		/// 
		/// var parser = new MathParser(false, true, false);
		/// parser.ProgrammaticallyParse("let my_var = 7");
		/// 
		/// Debug.Assert(parser.Parse("my_var - 3") == 4);
		/// </code>
		/// </example>
		/// <param name="mathExpression">The math expression to parse and evaluate.</param>
		/// <param name="correctExpression">
		/// If true, attempt to correct any typos found in the expression.
		/// </param>
		/// <param name="identifyComments">
		/// If true, treat "#" as a single-line comment and treat "#{" and "}#" as multi-line
		/// comments.
		/// </param>
		/// <returns>Returns the result of executing the given math expression.</returns>
		public double ProgrammaticallyParse(
			string mathExpression, bool correctExpression = true, bool identifyComments = true)
		{
			if (identifyComments)
			{
				// Delete Comments #{Comment}#
				mathExpression = Regex.Replace(mathExpression, "#\\{.*?\\}#", string.Empty);

				// Delete Comments #Comment
				mathExpression = Regex.Replace(mathExpression, "#.*$", string.Empty);
			}

			if (correctExpression)
			{
				// this refers to the Correction function which will correct stuff like artn to arctan, etc.
				mathExpression = Correction(mathExpression);
			}

			string varName;
			double varValue;

			if (mathExpression.Contains(VariableDeclarator))
			{
				if (mathExpression.Contains("be"))
				{
					varName = mathExpression.Substring(mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) + 3,
						mathExpression.IndexOf("be", StringComparison.Ordinal) -
						mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) - 3);
					mathExpression = mathExpression.Replace(varName + "be", string.Empty);
				}
				else
				{
					varName = mathExpression.Substring(mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) + 3,
						mathExpression.IndexOf("=", StringComparison.Ordinal) -
						mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) - 3);
					mathExpression = mathExpression.Replace(varName + "=", string.Empty);
				}

				varName = varName.Replace(" ", string.Empty);
				mathExpression = mathExpression.Replace(VariableDeclarator, string.Empty);

				varValue = Compute(mathExpression);

				if (variables.ContainsKey(varName))
				{
					variables[varName] = varValue;
				}
				else
				{
					variables.Add(varName, varValue);
				}

				return varValue;
			}

			if (!mathExpression.Contains(":="))
			{
				return Compute(mathExpression);
			}

			//mathExpression = mathExpression.Replace(" ", string.Empty); // remove white space
			varName = mathExpression.Substring(0, mathExpression.IndexOf(":=", StringComparison.Ordinal));
			mathExpression = mathExpression.Replace(varName + ":=", string.Empty);

			varValue = Compute(mathExpression);
			varName = varName.Replace(" ", string.Empty);

			if (variables.ContainsKey(varName))
			{
				variables[varName] = varValue;
			}
			else
			{
				variables.Add(varName, varValue);
			}

			return varValue;
		}


		// This will correct sqrt() and arctan() typos.
		private static string Correction(string input)
		{
			// Word corrections

			input = Regex.Replace(input, "\\b(sqr|sqrt)\\b", "sqrt", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\b(atan2|arctan2)\\b", "arctan2", RegexOptions.IgnoreCase);
			//... and more

			return input;
		}
		#endregion ProgrammaticallyParse


		private List<string> Parse(string expr)
		{
			Debug.WriteLine($"expression=[{expr}]");

			var token = string.Empty;
			var tokens = new List<string>();

			expr = expr.Replace("+-", "-");
			expr = expr.Replace("-+", "-");
			expr = expr.Replace("--", "+");
			expr = expr.Replace("==", "=");
			expr = expr.Replace(">=", "" + GeqSign);
			expr = expr.Replace("<=", "" + LeqSign);
			expr = expr.Replace("!=", "" + NeqSign);

			for (var i = 0; i < expr.Length; i++)
			{
				var ch = expr[i];

				if (char.IsWhiteSpace(ch))
				{
					continue;
				}

				if (char.IsLetter(ch))
				{
					if (i != 0 && (char.IsDigit(expr[i - 1]) || expr[i - 1] == ')'))
					{
						tokens.Add("*");
					}

					token += ch;

					while (i + 1 < expr.Length && char.IsLetterOrDigit(expr[i + 1]))
					{
						token += expr[++i];
					}

					tokens.Add(token);
					token = string.Empty;
					continue;
				}

				if (char.IsDigit(ch))
				{
					token += ch;

					while (i + 1 < expr.Length && (char.IsDigit(expr[i + 1]) || expr[i + 1] == '.'))
					{
						token += expr[++i];
					}

					tokens.Add(token);
					token = string.Empty;
					continue;
				}

				if (ch == '.')
				{
					token += ch;

					while (i + 1 < expr.Length && char.IsDigit(expr[i + 1]))
					{
						token += expr[++i];
					}

					tokens.Add(token);
					token = string.Empty;
					continue;
				}

				if (i + 1 < expr.Length &&
					(ch == '-' || ch == '+') &&
					char.IsDigit(expr[i + 1]) &&
					(i == 0 || (tokens.Count > 0 &&
						operators.ContainsKey(tokens[tokens.Count - 1])) ||
						i - 1 > 0 && expr[i - 1] == '('))
				{
					// if the above is true, then the token for that negative number will be
					// "-1", not "-","1". To sum up, the above will be true if the minus sign is
					// in front of the number, but at the beginning, for example, -1+2, or, when
					// it is inside the brakets (-1), or when it comes after another operator.
					// NOTE: this works for + as well!

					token += ch;

					while (i + 1 < expr.Length && (char.IsDigit(expr[i + 1]) || expr[i + 1] == '.'))
					{
						token += expr[++i];
					}

					tokens.Add(token);
					token = string.Empty;
					continue;
				}

				if (ch == '(')
				{
					if (i != 0 && (char.IsDigit(expr[i - 1]) || expr[i - 1] == ')'))
					{
						tokens.Add("*");
						tokens.Add("(");
					}
					else
					{
						tokens.Add("(");
					}
				}
				else
				{
					tokens.Add(ch.ToString());
				}
			}

			for (var i = 0; i < tokens.Count; i++)
			{
				Debug.WriteLine($"... tokens[{i}] = [{tokens[i]}]");
			}

			return tokens;
		}


		private void ReplaceVariables(List<string> tokens)
		{
			// Variables replacement
			for (var i = 0; i < tokens.Count; i++)
			{
				if (variables.Keys.Contains(tokens[i]))
				{
					tokens[i] = variables[tokens[i]].ToString(cultureInfo);
				}
				else if (
					tokens.Count == 1 ||
					(!(i > 0 && tokens[i - 1] == ":") &&
					!(i < tokens.Count - 1 && tokens[i + 1] == ":")))
				{
					var match = Regex.Match(tokens[i], AddressPattern);
					if (match.Success)
					{
						var value = GetCellContentInternal(tokens[i])
							?? throw new CalculatorException(
								$"invalid parameter at cell {tokens[i]}");

						tokens[i] = value;
					}
				}
			}
		}


		private void PreprocessTableFunctions(List<string> tokens)
		{
			var open = tokens.LastIndexOf("(");
			while (open > 0) // leave room for "cell" fn token prior to "("
			{
				var close = tokens.IndexOf(")", open);
				if (open >= close)
				{
					throw new CalculatorException($"No closing bracket/parenthesis. Token: {open}");
				}

				if (tokens[open - 1]
					.Equals(CellFnName, StringComparison.CurrentCultureIgnoreCase))
				{
					PreprocessCellFn(tokens, open, close);
				}
				else if (tokens[open - 1]
					.Equals(CountifFnName, StringComparison.CurrentCultureIgnoreCase))
				{
					PreprocessCountifFn(tokens, open, close);
				}

				open = tokens.LastIndexOf("(", open - 1);
			}

			for (var i = 0; i < tokens.Count; i++)
			{
				Debug.WriteLine($"... precell[{i}] = [{tokens[i]}]");
			}
		}


		private void PreprocessCellFn(List<string> tokens, int open, int close)
		{
			var lparams = new List<string>();
			var rparams = new List<string>();
			var commas = 0;
			for (var i = open + 1; i < close; i++)
			{
				if (tokens[i] == ",")
				{
					commas++;
				}
				else if (commas == 0)
				{
					lparams.Add(tokens[i]);
				}
				else
				{
					rparams.Add(tokens[i]);
				}
			}

			if (commas != 1)
			{
				throw new CalculatorException(
					$"The {CellFnName} function must have two parameters");
			}

			if (!variables.ContainsKey("col") || !variables.ContainsKey("row"))
			{
				throw new CalculatorException(
					$"The {CellFnName} function requires the col and row variables");
			}

			var currentCol = variables["col"];
			var currentRow = variables["row"];

			var col = (int)currentCol + (int)EvaluateBasicMathExpression(lparams);
			var row = (int)currentRow + (int)EvaluateBasicMathExpression(rparams);

			var cellName = $"{CellIndexToLetters(col)}{row}";

			tokens.RemoveRange(open - 1, close - open + 2);
			tokens.Insert(open - 1, cellName);
		}


		private static void PreprocessCountifFn(List<string> tokens, int open, int close)
		{
			var last = tokens.LastIndexOf(",", close);
			if (last > open)
			{
				var op = tokens[last + 1];
				// is there an explicit operator?
				if (op == ">" || op == "<" || op == "!")
				{
					if (op == ">") tokens[last + 1] = "1";
					else if (op == "<") tokens[last + 1] = "-1";
					else if (op == "!") tokens[last + 1] = "3";

					tokens.Insert(last + 2, ",");
				}
				else
				{
					// convert implicit equals to explicit
					tokens.Insert(last, "0");
					tokens.Insert(last, ",");
				}
			}
		}


		private void GetCellContents(List<string> tokens)
		{
			var pattern = new Regex(AddressPattern);

			var index = tokens.IndexOf(":");
			while (index != -1)
			{
				if (index == 0 || index == tokens.Count - 1)
				{
					throw new CalculatorException("invalid range");
				}

				// cells...

				var match = pattern.Match(tokens[index - 1]);
				if (!match.Success)
				{
					throw new CalculatorException(
						$"undefined cell ref [{tokens[index - 1]}]");// string.Format(ErrUndefinedSymbol, cell1), p1);
				}

				var col1 = match.Groups[1].Value.ToUpper();
				var row1 = match.Groups[2].Value;

				match = pattern.Match(tokens[index + 1]);
				if (!match.Success)
				{
					throw new CalculatorException(
						$"undefined cell ref [{tokens[index - 1]}]");//string.Format(ErrUndefinedSymbol, cell1), p1);
				}

				var col2 = match.Groups[1].Value.ToUpper();
				var row2 = match.Groups[2].Value;

				// expand...

				var values = new List<string>();
				if (col1 == col2)
				{
					var r1 = int.Parse(row1);
					var r2 = int.Parse(row2);
					if (r1 > r2)
					{
						var t = r1; r1 = r2; r2 = t;
					}

					// iterate rows in column
					for (var row = r1; row <= r2; row++)
					{
						var value = GetCellContentInternal($"{col1}{row}")
							?? throw new CalculatorException(
								$"invalid parameter at cell {col1}{row1}");

						if (values.Count > 0)
						{
							values.Add(",");
						}

						values.Add(value);
					}
				}
				else if (row1 == row2)
				{
					var c1 = CellLettersToIndex(col1);
					var c2 = CellLettersToIndex(col2);
					if (c1 > c2)
					{
						var t = c1; c1 = c2; c2 = t;
					}

					// iterate columns in row
					for (var col = c1; col <= c2; col++)
					{
						var v = GetCellContentInternal($"{CellIndexToLetters(col)}{row1}")
							?? throw new CalculatorException(
								$"invalid parameter at cell {CellIndexToLetters(col)}{row1}");

						if (values.Count > 0)
						{
							values.Add(",");
						}

						values.Add(v);
					}
				}
				else
				{
					throw new FormatException(
						$"invalid cell range {tokens[index - 1]}:{tokens[index + 1]}"); // ErrInvalidCellRange);
				}

				// replace token range with values

				if (values.Count > 0)
				{
					tokens.RemoveRange(index - 1, 3);
					tokens.InsertRange(index - 1, values);
					index += values.Count;
				}

				index = index < tokens.Count - 1 ? tokens.IndexOf(":", index + 1) : -1;
			}
		}


		private string GetCellContentInternal(string name)
		{
			// ask consumer to resolve cell reference
			if (GetCellValue != null)
			{
				var args = new GetCellValueEventArgs(name);
				GetCellValue(this, args);
				return args.Value;
			}

			return null;
		}


		private static string CellIndexToLetters(int index)
		{
			int div = index;
			string letters = string.Empty;
			int mod;

			while (div > 0)
			{
				mod = (div - 1) % 26;
				letters = $"{(char)(65 + mod)}{letters}";
				div = ((div - mod) / 26);
			}
			return letters;
		}


		private static int CellLettersToIndex(string letters)
		{
			letters = letters.ToUpper();
			int sum = 0;

			for (int i = 0; i < letters.Length; i++)
			{
				sum *= 26;
				sum += (letters[i] - 'A' + 1);
			}
			return sum;
		}


		private double Evaluate(List<string> tokens)
		{
			var open = tokens.LastIndexOf("(");
			while (open != -1)
			{
				// getting data between "(" and ")"
				var close = tokens.IndexOf(")", open
					); // incase open is -1, i.e. no "(" // , open == 0 ? 0 : open - 1

				if (open >= close)
				{
					throw new ArithmeticException(
						$"no closing bracket/parenthesis matching index {open}");
				}

				double result;

				// parenthetical elements
				var elements = new List<string>();
				for (var i = open + 1; i < close; i++)
				{
					elements.Add(tokens[i]);
				}

				var name = tokens[open == 0 ? 0 : open - 1];
				var func = functions.Keys.Contains(name)
					? functions[name]
					: factory.Find(name);

				if (func is null)
				{
					// no function, just simple math
					result = EvaluateBasicMathExpression(elements);
				}
				else if (elements.Count == 0)
				{
					result = func(new VariantList());
				}
				else
				{
					var vargs = new VariantList();
					var i = 0;
					while (i < elements.Count)
					{
						var expr = new List<string>();
						var nextComma = elements.IndexOf(",", i);
						var end = nextComma != -1 ? nextComma : elements.Count;
						while (i < end)
						{
							expr.Add(elements[i++]);
						}

						// this is an exception case for countif where the first n parameters and
						// the last parameter can be true/false/string
						if (name == CountifFnName &&
							expr.Count == 1 &&
							!double.TryParse(expr[0], NumberStyles.Number, cultureInfo, out _))
						{
							// countif match paramter
							vargs.Add(new Variant(expr[0]));
						}
						else
						{
							var r = expr.Count == 0 ? 0 : EvaluateBasicMathExpression(expr);
							vargs.Add(new Variant(r));
						}

						i++;
					}

					result = func(vargs);
				}

				// when all calcs are done, replace opening bracket with result and remove rest
				tokens[open] = result.ToString(cultureInfo);
				tokens.RemoveRange(open + 1, close - open);

				if (func is not null)
				{
					// remove function name as well
					tokens.RemoveAt(open - 1);
				}

				open = tokens.LastIndexOf("(");
			}

			// at this point, we should have replaced all brackets with the appropriate values,
			// so we can simply calculate the expression
			return EvaluateBasicMathExpression(tokens);
		}


		private double EvaluateBasicMathExpression(List<string> tokens)
		{
			// PERFORMING A BASIC ARITHMETICAL EXPRESSION CALCULATION. THIS METHOD CAN ONLY
			// OPERATE WITH NUMBERS AND OPERATORS AND WILL NOT UNDERSTAND ANYTHING BEYOND THAT.

			double token0;
			double token1;

			switch (tokens.Count)
			{
				case 1:
					if (!double.TryParse(tokens[0], NumberStyles.Number, cultureInfo, out token0))
					{
						throw new CalculatorException($"variable {tokens[0]} is undefined");
					}

					return token0;

				case 2:
					var op = tokens[0];

					if (op == "-" || op == "+")
					{
						var first = op == "+"
							? string.Empty
							: tokens[1].Substring(0, 1) == "-" ? string.Empty : "-";

						if (!double.TryParse(first + tokens[1], NumberStyles.Number, cultureInfo, out token1))
						{
							throw new CalculatorException($"variable {first + tokens[1]} is undefined");
						}

						return token1;
					}

					if (!operators.ContainsKey(op))
					{
						throw new CalculatorException($"operator {op} is undefined");
					}

					if (!double.TryParse(tokens[1], NumberStyles.Number, cultureInfo, out token1))
					{
						throw new CalculatorException($"variable {tokens[1]} is undefined");
					}

					return operators[op](0, token1);

				case 0:
					return 0;
			}

			foreach (var op in operators)
			{
				int opPlace;

				while ((opPlace = tokens.IndexOf(op.Key)) != -1)
				{
					if (!double.TryParse(tokens[opPlace + 1], NumberStyles.Number, cultureInfo, out var rhs))
					{
						throw new CalculatorException($"variable {tokens[opPlace + 1]} is undefined");
					}

					if (op.Key == "-" && opPlace == 0)
					{
						var result = op.Value(0.0, rhs);
						tokens[0] = result.ToString(cultureInfo);
						tokens.RemoveRange(opPlace + 1, 1);
					}
					else
					{
						if (!double.TryParse(tokens[opPlace - 1], NumberStyles.Number, cultureInfo, out var lhs))
						{
							throw new CalculatorException($"variable {tokens[opPlace - 1]} is undefined");
						}

						var result = op.Value(lhs, rhs);
						tokens[opPlace - 1] = result.ToString(cultureInfo);
						tokens.RemoveRange(opPlace, 2);
					}
				}
			}

			if (!double.TryParse(tokens[0], NumberStyles.Number, cultureInfo, out token0))
			{
				throw new CalculatorException($"variable {tokens[0]} is undefined");
			}

			return token0;
		}
	}
}
