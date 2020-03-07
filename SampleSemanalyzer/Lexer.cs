using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;

namespace SampleSemanalyzer
{
    public class Token
    {
        public string Lexeme { get; set; }
        public string Category { get; set; }

        public override string ToString() => $"({Category}, {Lexeme})";
    }

    public static class Lexer
    {
        public static IEnumerable<Token> Lex(string input)
        {
            var patDict = new[]
            {
                (@"[a-zA-Z]+", "id"),
                (@"[0-9]+", "num"),
                (@"[0-9]+\.[0-9]+", "float"),
                (@"[+\-]", "addop"),
                (@"[*/]", "mulop"),
                (@"int|float|bool|none", "primitive"),
                (@"and|or|xor|nor|nand", "boolop"),
                (@">=|<=|==|<|>|!=", "relop"),
                (@"if|then|fi|while|do|done|let|fun|begin|end|return|<-|->|\(|\)|\[|\]|;|:|,", null)
            }.Select(x => (new Regex(x.Item1), x.Item2)).ToList();

            foreach (var word in input.Split(' ', '\t', '\n'))
            {
                var buf = word;

                while (buf != "")
                {
                    var longest = ("Error", buf.Substring(0, 1));
                    
                    foreach (var x in patDict)
                    {
                        var (re, category) = x;
                        var mat = re.Match(buf);
                        if (mat.Success && mat.Index == 0 && mat.Length >= longest.Item2.Length)
                        {
                            longest = (category ?? buf.Substring(0, mat.Length), buf.Substring(0, mat.Length));
                        }
                    }

                    yield return new Token
                    {
                        Category = longest.Item1,
                        Lexeme = longest.Item2
                    };

                    buf = buf.Substring(longest.Item2.Length);
                }
            }
        }
    }
}