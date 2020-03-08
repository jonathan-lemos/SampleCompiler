using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SampleSemanalyzer;

namespace SampleSemanalyzerTests
{
    public class Tests
    {
        private static string[] _accept =
        {
            @"print(x: 2);",
            @" print ( x : 2 ) ; ",
            
            @"
fun square(x: int) : int <- begin
    return x * x;
end

print(x: square(x: 4));
",
            
            @"
let funding: int <- 4;
funding <- 5;

fun thing() : int <- begin
    funding <- 6;
    return funding;
end

thing();
print(x: funding);
"
        };

        private static string[] _reject =
        {
            @"print(2);",
            @"print(x: 2)"
        };

        [Test]
        [TestCaseSource(nameof(_accept))]
        public void AcceptTest(string input)
        {
            Assert.DoesNotThrow(() =>
            {
                var tokens = Lexer.Lex(input).ToList();
                var tree = Parser.ParseStart(new Queue<Token>(tokens));
                tree.Verify();
            });
        }

        [Test]
        [TestCaseSource(nameof(_reject))]
        public void RejectTest(string input)
        {
            Assert.That(() =>
            {
                var tokens = Lexer.Lex(input).ToList();
                var tree = Parser.ParseStart(new Queue<Token>(tokens));
                tree.Verify();
            }, Throws.Exception);
        }
    }
}