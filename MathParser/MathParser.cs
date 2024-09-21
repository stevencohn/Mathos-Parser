/* 
 * Copyright (C) 2012-2019, Mathos Project.
 * All rights reserved.
 * 
 * Please see the license file in the project folder
 * or go to https://github.com/MathosProject/Mathos-Parser/blob/master/LICENSE.md.
 * 
 * Please feel free to ask me directly at my email!
 *  artem@artemlos.net
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mathos.Parser
{
	internal delegate void GetCellValueHandler(object sender, GetCellValueEventArgs e);


	internal class GetCellValueEventArgs : EventArgs
	{
		public GetCellValueEventArgs(string name)
		{
			Name = name;
		}
		public string Name { get; private set; }
		public string Value { get; set; }
	}


	/// <summary>
	/// A mathematical expression parser and evaluator.
	/// </summary>
	/// <remarks>
	/// This is considered the default parser for mathematical expressions and provides baseline functionality.
	/// For more specialized parsers, see <seealso cref="BooleanParser"/> and <seealso cref="Scripting.ScriptParser"/>.
	/// </remarks>
	internal class MathParser
	{
		/// <summary>
		/// Regex pattern for matching cell addresses of the form [col-letters][row-number] where
		/// row-number is a positive, non-zero integer. Capture groups are named c)ell and r)row.
		/// </summary>
		private const string AddressPattern = @"^(?<c>[a-zA-Z]+)(?<r>\d+)$";
		private const string CellFnName = "cell";
		private const string CountifFnName = "countif";

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


		public event GetCellValueHandler GetCellValue;



		/// <summary>
		/// Iniitalizes a new math parser.
		/// </summary>
		public MathParser()
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
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="fn"></param>
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
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="fn"></param>
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
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public double GetVariable(string name)
		{
			if (variables.ContainsKey(name))
			{
				return variables[name];
			}

			return double.NaN;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
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
		/// For a method that does, please use <see cref="ProgrammaticallyParse"/>.
		/// </remarks>
		/// <example>
		/// <code>
		/// using System.Diagnostics;
		/// 
		/// var parser = new MathParser(false, true, false);
		/// Debug.Assert(parser.Parse("2 + 2") == 4);
		/// </code>
		/// </example>
		/// <param name="mathExpression">The math expression to parse and evaluate.</param>
		/// <returns>Returns the result of executing the given math expression.</returns>
		public double Parse(string mathExpression)
		{
			var tokens = Lexer(mathExpression);

			ReplaceVariables(tokens);

			if (tokens.Exists(t => t.Equals(CountifFnName, StringComparison.CurrentCultureIgnoreCase)))
			{
				PreprocessCountifFn(tokens);
			}

			if (tokens.Exists(t => t.Equals(CellFnName, StringComparison.CurrentCultureIgnoreCase)))
			{
				PreprocessCellFn(tokens);
			}

			GetCellContents(tokens);
			return MathParserLogic(tokens);
		}


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
				mathExpression = Regex.Replace(mathExpression, "#\\{.*?\\}#", "");

				// Delete Comments #Comment
				mathExpression = Regex.Replace(mathExpression, "#.*$", "");
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
					mathExpression = mathExpression.Replace(varName + "be", "");
				}
				else
				{
					varName = mathExpression.Substring(mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) + 3,
						mathExpression.IndexOf("=", StringComparison.Ordinal) -
						mathExpression.IndexOf(VariableDeclarator, StringComparison.Ordinal) - 3);
					mathExpression = mathExpression.Replace(varName + "=", "");
				}

				varName = varName.Replace(" ", "");
				mathExpression = mathExpression.Replace(VariableDeclarator, "");

				varValue = Parse(mathExpression);

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
				return Parse(mathExpression);
			}

			//mathExpression = mathExpression.Replace(" ", ""); // remove white space
			varName = mathExpression.Substring(0, mathExpression.IndexOf(":=", StringComparison.Ordinal));
			mathExpression = mathExpression.Replace(varName + ":=", "");

			varValue = Parse(mathExpression);
			varName = varName.Replace(" ", "");

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


		#region Core

		// This will correct sqrt() and arctan() typos.
		private string Correction(string input)
		{
			// Word corrections

			input = Regex.Replace(input, "\\b(sqr|sqrt)\\b", "sqrt", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\b(atan2|arctan2)\\b", "arctan2", RegexOptions.IgnoreCase);
			//... and more

			return input;
		}

		private List<string> Lexer(string expr)
		{
			Debug.WriteLine($"expression=[{expr}]");

			var token = "";
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
					token = "";

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
					token = "";

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
					token = "";

					continue;
				}

				if (i + 1 < expr.Length &&
					(ch == '-' || ch == '+') &&
					char.IsDigit(expr[i + 1]) &&
					(i == 0 || (tokens.Count > 0 &&
						operators.ContainsKey(tokens.Last())) || i - 1 > 0 && expr[i - 1] == '('))
				{
					// if the above is true, then the token for that negative number will be "-1", not "-","1".
					// to sum up, the above will be true if the minus sign is in front of the number, but
					// at the beginning, for example, -1+2, or, when it is inside the brakets (-1), or when it comes after another operator.
					// NOTE: this works for + as well!

					token += ch;

					while (i + 1 < expr.Length && (char.IsDigit(expr[i + 1]) || expr[i + 1] == '.'))
					{
						token += expr[++i];
					}

					tokens.Add(token);
					token = "";

					continue;
				}

				if (ch == '(')
				{
					if (i != 0 && (char.IsDigit(expr[i - 1]) || char.IsDigit(expr[i - 1]) || expr[i - 1] == ')'))
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
						var value = GetCellContentInternal(tokens[i]);
						if (value is null)
						{
							throw new CalculatorException(
								$"invalid parameter at cell {tokens[i]}");
						}

						tokens[i] = value;
					}
				}
			}
		}


		private void PreprocessCellFn(List<string> tokens)
		{
			var open = tokens.LastIndexOf("(");
			while (open > 0) // leave room for "cell" fn token prior to "("
			{
				var close = tokens.IndexOf(")", open);
				if (open >= close)
				{
					throw new CalculatorException($"No closing bracket/parenthesis. Token: {open}");
				}

				if (tokens[open - 1].Equals(CellFnName, StringComparison.CurrentCultureIgnoreCase))
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

					var col = (int)currentCol + (int)BasicArithmeticalExpression(lparams);
					var row = (int)currentRow + (int)BasicArithmeticalExpression(rparams);

					var cellName = $"{CellIndexToLetters(col)}{row}";

					tokens.RemoveRange(open - 1, close - open + 2);
					tokens.Insert(open - 1, cellName);
				}

				open = tokens.LastIndexOf("(", open - 1);
			}

			for (var i = 0; i < tokens.Count; i++)
			{
				Debug.WriteLine($"... precell[{i}] = [{tokens[i]}]");
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
						$"undefined cell ref [{tokens[index - 1]}]");//string.Format(ErrUndefinedSymbol, cell1), p1);
				}

				var col1 = match.Groups[1].Value;
				var row1 = match.Groups[2].Value;

				match = pattern.Match(tokens[index + 1]);
				if (!match.Success)
				{
					throw new CalculatorException(
						$"undefined cell ref [{tokens[index - 1]}]");//string.Format(ErrUndefinedSymbol, cell1), p1);
				}

				var col2 = match.Groups[1].Value;
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


		private void PreprocessCountifFn(List<string> tokens)
		{
			var open = tokens.LastIndexOf("(");
			while (open > 0) // leave room for fn name token prior to "("
			{
				var close = tokens.IndexOf(")", open);
				if (open >= close)
				{
					throw new CalculatorException($"No closing bracket/parenthesis. Token: {open}");
				}

				// is this the fn name we're looking for?
				if (tokens[open - 1].Equals(CountifFnName, StringComparison.CurrentCultureIgnoreCase))
				{
					var last = tokens.LastIndexOf(",", close);
					if (last > open)
					{
						var op = tokens[last - 1][0];
						if (op == '>') tokens[last - 1] = "1";
						else if (op == '<') tokens[last - 1] = "-1";
						else if (op == '!') tokens[last - 1] = "3";
						else
						{
							op = tokens[last + 1][0];
							if (op == '>' || op == '<' || op == '!')
							{
								if (op == '>') tokens.Insert(last, "1");
								else if (op == '<') tokens.Insert(last, "-1");
								else if (op == '!') tokens.Insert(last, "3");
								else tokens.Insert(last, "0");
								tokens.Insert(last, ",");
							}
							else
							{
								// insert implied String.Equals
								tokens.Insert(last, "0");
								tokens.Insert(last, ",");
							}
						}
					}
				}

				open = tokens.LastIndexOf("(", open - 1);
			}

			for (var i = 0; i < tokens.Count; i++)
			{
				Debug.WriteLine($"... precountif[{i}] = [{tokens[i]}]");
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


		private double MathParserLogic(List<string> tokens)
		{
			var open = tokens.LastIndexOf("(");
			while (open != -1)
			{
				// getting data between "(" and ")"
				var close = tokens.IndexOf(")", open); // incase open is -1, i.e. no "(" // , open == 0 ? 0 : open - 1

				if (open >= close)
				{
					throw new ArithmeticException("No closing bracket/parenthesis. Token: " + open.ToString(cultureInfo));
				}

				var subexpr = new List<string>();
				for (var i = open + 1; i < close; i++)
				{
					subexpr.Add(tokens[i]);
				}

				double result;

				var args = new List<double>();
				var functionName = tokens[open == 0 ? 0 : open - 1];

				// looks for built-in overrides first before user-defined functions
				var fn = functions.Keys.Contains(functionName)
					? functions[functionName]
					: factory.Find(functionName);

				if (fn is not null)
				{
					if (subexpr.Contains(","))
					{
						// converting all arguments into a double array
						for (var i = 0; i < subexpr.Count; i++)
						{
							var defaultExpr = new List<string>();
							var firstCommaOrEndOfExpression =
								subexpr.IndexOf(",", i) != -1
									? subexpr.IndexOf(",", i)
									: subexpr.Count;

							while (i < firstCommaOrEndOfExpression)
							{
								defaultExpr.Add(subexpr[i++]);
							}

							args.Add(defaultExpr.Count == 0 ? 0 : BasicArithmeticalExpression(defaultExpr));
						}

						// finally, passing the arguments to the given function
						result = double.Parse(
							fn(new VariantList(args)).ToString(cultureInfo),
							cultureInfo);
					}
					else
					{
						if (subexpr.Count == 0)
						{
							result = fn(new VariantList());
						}
						else
						{
							result = double.Parse(
								fn(new VariantList(BasicArithmeticalExpression(subexpr))
							).ToString(cultureInfo), cultureInfo);
						}
					}
				}
				else
				{
					// if no function is need to execute following expression, pass it
					// to the "BasicArithmeticalExpression" method.
					result = BasicArithmeticalExpression(subexpr);
				}

				// when all the calculations have been done
				// we replace the "opening bracket with the result"
				// and removing the rest.
				tokens[open] = result.ToString(cultureInfo);
				tokens.RemoveRange(open + 1, close - open);

				if (fn is not null)
				{
					// if we also executed a function, removing
					// the function name as well.
					tokens.RemoveAt(open - 1);
				}

				open = tokens.LastIndexOf("(");
			}

			// at this point, we should have replaced all brackets
			// with the appropriate values, so we can simply
			// calculate the expression. it's not so complex
			// any more!
			return BasicArithmeticalExpression(tokens);
		}


		private double BasicArithmeticalExpression(List<string> tokens)
		{
			// PERFORMING A BASIC ARITHMETICAL EXPRESSION CALCULATION
			// THIS METHOD CAN ONLY OPERATE WITH NUMBERS AND OPERATORS
			// AND WILL NOT UNDERSTAND ANYTHING BEYOND THAT.

			double token0;
			double token1;

			switch (tokens.Count)
			{
				case 1:
					if (!double.TryParse(tokens[0], NumberStyles.Number, cultureInfo, out token0))
					{
						throw new CalculatorException("local variable " + tokens[0] + " is undefined");
					}

					return token0;
				case 2:
					var op = tokens[0];

					if (op == "-" || op == "+")
					{
						var first = op == "+" ? "" : (tokens[1].Substring(0, 1) == "-" ? "" : "-");

						if (!double.TryParse(first + tokens[1], NumberStyles.Number, cultureInfo, out token1))
						{
							throw new CalculatorException("local variable " + first + tokens[1] + " is undefined");
						}

						return token1;
					}

					if (!operators.ContainsKey(op))
					{
						throw new CalculatorException("operator " + op + " is not defined");
					}

					if (!double.TryParse(tokens[1], NumberStyles.Number, cultureInfo, out token1))
					{
						throw new CalculatorException("local variable " + tokens[1] + " is undefined");
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
					double rhs;

					if (!double.TryParse(tokens[opPlace + 1], NumberStyles.Number, cultureInfo, out rhs))
					{
						throw new CalculatorException("local variable " + tokens[opPlace + 1] + " is undefined");
					}

					if (op.Key == "-" && opPlace == 0)
					{
						var result = op.Value(0.0, rhs);
						tokens[0] = result.ToString(cultureInfo);
						tokens.RemoveRange(opPlace + 1, 1);
					}
					else
					{
						double lhs;

						if (!double.TryParse(tokens[opPlace - 1], NumberStyles.Number, cultureInfo, out lhs))
						{
							throw new CalculatorException("local variable " + tokens[opPlace - 1] + " is undefined");
						}

						var result = op.Value(lhs, rhs);
						tokens[opPlace - 1] = result.ToString(cultureInfo);
						tokens.RemoveRange(opPlace, 2);
					}
				}
			}

			if (!double.TryParse(tokens[0], NumberStyles.Number, cultureInfo, out token0))
			{
				throw new CalculatorException("local variable " + tokens[0] + " is undefined");
			}

			return token0;
		}

		#endregion
	}
}
