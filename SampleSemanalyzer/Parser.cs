using System;
using System.Collections.Generic;
using System.Linq;

namespace SampleSemanalyzer
{
    public static class VarTab
    {
        public static readonly IList<IDictionary<string, TypeSpec>> Scopes =
            new List<IDictionary<string, TypeSpec>>
            {
                new Dictionary<string, TypeSpec>()
            };

        public static void Clear()
        {
            Scopes.Clear();
            PushScope();
        }

        public static void PushScope()
        {
            Scopes.Add(new Dictionary<string, TypeSpec>());
        }

        public static void PopScope()
        {
            Scopes.RemoveAt(Scopes.Count - 1);
        }

        public static void Add(string id, TypeSpec type)
        {
            if (Scopes.Last().ContainsKey(id))
            {
                throw new ArgumentException(id);
            }

            Scopes.Last()[id] = type;
        }

        public static TypeSpec Get(string id) =>
            Scopes.Reverse().First(scope => scope.ContainsKey(id))[id];
    }

    public static class FuncState
    {
        public readonly static Stack<TypeSpec> CurrentReturn = new Stack<TypeSpec>();

        public static void Clear() => CurrentReturn.Clear();

        public static void PushFunc(TypeSpec returnType) => CurrentReturn.Push(returnType);

        public static TypeSpec PopFunc() => CurrentReturn.Pop();

        public static TypeSpec Top => CurrentReturn.Peek();
    }

    public class Start
    {
        public IList<Stmt> Statements { get; set; }

        public void Verify()
        {
            Statements.ForEach(stmt => stmt.Verify());
        }
    }

    public interface Stmt
    {
        void Verify();
    }

    public class IfStmt : Stmt
    {
        public Condition Condition { get; set; }
        public IList<Stmt> IfBlock { get; set; }
        public IList<Stmt>? ElseBlock { get; set; }

        public void Verify()
        {
            Condition.Verify();
            IfBlock.ForEach(stmt => stmt.Verify());
            ElseBlock.ForEach(stmt => stmt.Verify());
        }
    }

    public class WhileStmt : Stmt
    {
        public Condition Condition { get; set; }
        public IList<Stmt> Statements { get; set; }

        public void Verify()
        {
            Condition.Verify();
            Statements.ForEach(stmt => stmt.Verify());
        }
    }

    public class AssgnStmt : Stmt
    {
        public string Id { get; set; }
        public Expr Expr { get; set; }

        public void Verify()
        {
            var exprType = Expr.Verify();
            var assgnType = VarTab.Get(Id);
            if (exprType != assgnType)
            {
                throw new ArgumentException(assgnType.ToString());
            }
        }
    }

    public class ReturnStmt : Stmt
    {
        public Expr Expr { get; set; }

        public void Verify()
        {
            var retType = FuncState.Top;
            var exprType = Expr.Verify();
            if (retType != exprType)
            {
                throw new ArgumentException(retType.ToString());
            }
        }
    }

    public class VardecStmt : Stmt
    {
        public string Id { get; set; }
        public TypeSpec Type { get; set; }
        public Expr Expr { get; set; }

        public void Verify()
        {
            var exprType = Expr.Verify();
            if (Type != exprType)
            {
                throw new ArgumentException(Type.ToString());
            }

            VarTab.Add(Id, exprType);
        }
    }

    public class Param
    {
        public string Id { get; set; }
        public TypeSpec Type { get; set; }
        public Expr? Default { get; set; }

        public void Verify()
        {
            if (Default == null)
            {
                return;
            }

            var exprType = Default.Verify();
            if (Type != exprType)
            {
                throw new ArgumentException();
            }
        }
    }

    public class TypeSpec
    {
        public TypeBase Base { get; set; }
        public int ArrayCount { get; set; }

        public static bool operator ==(TypeSpec t1, TypeSpec t2)
        {
            if (ReferenceEquals(t1, t2)) return true;
            if (ReferenceEquals(t1, null)) return false;
            // to shut up resharper
            if (ReferenceEquals(t2, null)) return false;

            if (t1.ArrayCount != t2.ArrayCount) return false;

            switch (t1.Base)
            {
                case Primitive p1:
                {
                    if (!(t2.Base is Primitive p2))
                    {
                        return false;
                    }

                    return p1.Type == "ANY" || p2.Type == "ANY" || p1.Type == p2.Type;
                }
                case Function f1:
                {
                    if (!(t2.Base is Function f2))
                    {
                        return false;
                    }

                    return f1.Types.SequenceEqual(f2.Types) && f1.ReturnType == f2.ReturnType;
                }
                default:
                    return false;
            }
        }

        public static bool operator !=(TypeSpec t1, TypeSpec t2) => !(t1 == t2);
    }

    public interface TypeBase
    {
    }

    public class Primitive : TypeBase
    {
        public string Type { get; set; }
    }

    public class Function : TypeBase
    {
        public TypeSpec ReturnType { get; set; }
        public IList<TypeSpec> Types { get; set; }
    }

    public interface Expr
    {
        TypeSpec Verify();
    }

    public class Condition : Expr
    {
        public Expr Left { get; set; }
        public string Relop { get; set; }
        public Expr Right { get; set; }
        public string? Binop { get; set; }
        public Condition? Next { get; set; }

        public TypeSpec Verify()
        {
            var lhsType = Left.Verify();
            var rhsType = Right.Verify();

            if (lhsType != rhsType)
            {
                throw new ArgumentException(lhsType.ToString());
            }

            if ((Relop.Contains("<") || Relop.Contains(">")) && lhsType.ArrayCount != 0)
            {
                throw new ArgumentException();
            }

            Next?.Verify();

            return new TypeSpec
            {
                ArrayCount = 0,
                Base = new Primitive
                {
                    Type = "bool"
                }
            };
        }
    }

    public class AddExpr : Expr
    {
        public Term Term { get; set; }
        public string? Addop { get; set; }
        public AddExpr? Next { get; set; }

        public TypeSpec Verify()
        {
            var termType = Term.Verify();
            var nextType = Next?.Verify();
            if (nextType != null && termType != nextType)
            {
                throw new ArgumentException();
            }

            if (nextType != null && termType.ArrayCount != 0)
            {
                throw new ArgumentException();
            }

            return termType;
        }
    }

    public class ArrayExpr : Expr
    {
        public IList<Expr> Entries { get; set; }

        public TypeSpec Verify()
        {
            var types = Entries.Select(x => x.Verify()).ToList();
            if (types.ToHashSet().Count > 1)
            {
                throw new ArgumentException();
            }

            return types.ToHashSet().Count == 1
                ? new TypeSpec
                {
                    ArrayCount = types.First().ArrayCount + 1,
                    Base = types.First().Base
                }
                : new TypeSpec
                {
                    ArrayCount = 1,
                    Base = new Primitive
                    {
                        Type = "ANY"
                    }
                };
        }
    }

    public class FuncExpr : Expr
    {
        public IList<Param> Params { get; set; }
        public TypeBase ReturnType { get; set; }
        public IList<Stmt> Statements { get; set; }

        public TypeSpec Verify()
        {
            throw new NotImplementedException();
        }
    }

    public class Term
    {
        public Factor Factor { get; set; }
        public string? Mulop { get; set; }
        public Term? Next { get; set; }

        public TypeSpec Verify()
        {
            throw new NotImplementedException();
        }
    }

    public interface Factor
    {
        TypeBase Verify();
    }

    public class Var : Factor
    {
        public string Id { get; set; }
        public IList<Expr> ArrayIndex { get; set; }

        public TypeBase Verify()
        {
            throw new NotImplementedException();
        }
    }

    public class Call : Factor
    {
        public string Id { get; set; }
        public IList<Arg> Args { get; set; }

        public TypeBase Verify()
        {
            throw new NotImplementedException();
        }
    }

    public class Num : Factor
    {
        public int Num { get; set; }
    }

    public class Float : Factor
    {
        public double Float { get; set; }
    }

    public class ParenFactor : Factor
    {
        public Expr Expr { get; set; }
    }

    public class Arg
    {
        public string Id { get; set; }
        public Expr Expr { get; set; }
    }

    public class Parser
    {
        private readonly Queue<Token> _tokens;

        private Token Read(string category)
        {
            if (_tokens.Peek().Category != category)
            {
                throw new ArgumentException(category);
            }

            return _tokens.Dequeue();
        }

        private Token Next => _tokens.Peek();

        private Parser(Queue<Token> tokens)
        {
            _tokens = tokens;
        }

        public static Start ParseStart(Queue<Token> tokens)
        {
            var parser = new Parser(tokens);

            var stmts = parser.ParseStmtList();
            if (tokens.Any())
            {
                throw new ArgumentException(tokens.First().ToString());
            }

            return new Start
            {
                Statements = stmts
            };
        }

        public IList<Stmt> ParseStmtList()
        {
            var ret = new List<Stmt>();
            while (true)
            {
                try
                {
                    ret.Add(ParseStmt());
                }
                catch (ArgumentException)
                {
                    return ret;
                }
            }
        }

        public Stmt ParseStmt() => Next.Category switch
        {
            // cast is needed or C# "cannot find the best common type"
            "if" => (Stmt) ParseIfStmt(),
            "while" => ParseWhileStmt(),
            "return" => ParseReturnStmt(),
            "let" => ParseVardecStmt(),
            _ => throw new ArgumentException(Next.ToString())
        };

        public IfStmt ParseIfStmt()
        {
            Read("if");
            var cond = ParseCondition();
            Read("then");
            var stmt = ParseStmt();
            if (Next.Category == "else")
            {
                Read("else");
                var elseStmt = ParseStmt();
                Read("fi");
                return new IfStmt
                {
                    Condition = cond,
                    IfBlock = stmt,
                    ElseBlock = elseStmt
                };
            }

            Read("fi");
            return new IfStmt
            {
                Condition = cond,
                IfBlock = stmt
            };
        }

        public WhileStmt ParseWhileStmt()
        {
            Read("while");
            var cond = ParseCondition();
            Read("do");
            var stmt = ParseStmt();
            Read("done");
            return new WhileStmt
            {
                Condition = cond,
                Statements = stmt
            };
        }

        public AssgnStmt ParseAssgnStmt()
        {
            var id = Read("id");
            Read("<-");
            var expr = ParseExpr();
            Read(";");
            return new AssgnStmt
            {
                Id = id.Lexeme,
                Expr = expr
            };
        }

        public ReturnStmt ParseReturnStmt()
        {
            Read("return");
            var expr = ParseExpr();
            Read(";");
            return new ReturnStmt
            {
                Expr = expr
            };
        }

        public VardecStmt ParseVardecStmt()
        {
            Read("let");
            var id = Read("id");
            Read(":");
            var type = ParseTypeSpec();
            Read("<-");
            var expr = ParseExpr();
            return new VardecStmt
            {
                Id = id.Lexeme,
                Type = type,
                Expr = expr
            };
        }

        public TypeSpec ParseTypeSpec()
        {
            var arrayCounter = 0;
            while (Next.Category == "[")
            {
                Read("[");
                arrayCounter++;
            }

            var typeBase = ParseTypeBase();

            Enumerable.Range(0, arrayCounter).ForEach(_ => Read("]"));

            return new TypeSpec
            {
                ArrayCount = arrayCounter,
                Base = typeBase
            };
        }

        public TypeBase ParseTypeBase()
        {
            switch (Next.Category)
            {
                case "primitive":
                    return new Primitive
                    {
                        Type = Read("primitive").Lexeme
                    };
                case "(":
                {
                    Read("(");
                    var types = ParseTypes();
                    Read(")");
                    Read(":");
                    var typeSpec = ParseTypeSpec();
                    return new Function
                    {
                        Types = types,
                        ReturnType = typeSpec
                    };
                }
                default:
                    throw new ArgumentException(Next.ToString());
            }
        }

        public IList<TypeSpec> ParseTypes()
        {
            var ret = new List<TypeSpec>();
            while (true)
            {
                try
                {
                    ret.Add(ParseTypeSpec());
                }
                catch (ArgumentException)
                {
                    return ret;
                }
            }
        }

        public Expr ParseExpr() => Next.Category switch
        {
            "[" => ParseArrayExpr(),
            "(" => ParseFuncExpr(),
            _ => ParseCondition()
        };

        public ArrayExpr ParseArrayExpr()
        {
            Read("[");
            var args = new List<Expr>();
            while (true)
            {
                args.Add(ParseExpr());
                if (Next.Category != ",")
                {
                    Read("]");
                    break;
                }
                else
                {
                    Read(",");
                }
            }

            return new ArrayExpr
            {
                Entries = args
            };
        }

        public FuncExpr ParseFuncExpr()
        {
            Read("(");
            var par = ParseParams();
            Read(")");
            Read(":");
            var type = ParseTypeSpec();
            Read("<-");
            Read("{");
            var stmts = ParseStmtList();
            Read("}");
            return new FuncExpr
            {
                Params = par,
                ReturnType = type,
                Statements = stmts
            };
        }

        public IList<Param> ParseParams()
        {
            var ret = new List<Param>();
            while (Next.Category != ")")
            {
                ret.Add(ParseParam());
                if (Next.Category != ",")
                {
                    break;
                }

                Read(",");
            }

            return ret;
        }

        public Param ParseParam()
        {
            var id = Read("id").Lexeme;
            Read(":");
            var type = ParseTypeSpec();

            if (Next.Category == "<-")
            {
                Read("<-");
                var expr = ParseExpr();
                return new Param
                {
                    Id = id,
                    Type = type,
                    Default = expr
                };
            }

            return new Param
            {
                Id = id,
                Type = type
            };
        }

        public Expr ParseCondition()
        {
            var addExpr = ParseAddExpr();
        }

        public AddExpr ParseAddExpr()
        {
            var term = ParseTerm();
            if (Next.Category == "addop")
            {
                var addop = Read("addop").Lexeme;
                var addexpr = ParseAddExpr();
                return new AddExpr
                {
                    Term = term,
                    Addop = addop,
                    Next = addexpr
                };
            }

            return new AddExpr
            {
                Term = term
            };
        }

        public Term ParseTerm()
        {
            var factor = ParseFactor();
            if (Next.Category == "factor")
            {
                var mulop = Read("mulop").Lexeme;
                var term = ParseTerm();
                return new Term
                {
                    Factor = factor,
                    Mulop = mulop,
                    Next = term
                };
            }

            return new Term
            {
                Factor = factor
            };
        }

        public Factor ParseFactor()
        {
            switch (Next.Category)
            {
                case "id":
                    return ParseIdFactor();
                case "num":
                    return new Num {Num = int.Parse(Read("int").Lexeme)};
                case "float":
                    return new Float {Float = double.Parse(Read("float").Lexeme)};
                case "(":
                {
                    Read("(");
                    var expr = ParseExpr();
                    Read(")");
                    return new ParenFactor
                    {
                        Expr = expr
                    };
                }
                default:
                    throw new ArgumentException(Next.ToString());
            }
        }

        public Factor ParseIdFactor()
        {
            var id = Read("id").Lexeme;
            switch (Next.Category)
            {
                case "[":
                {
                    var indices = new List<Expr>();
                    while (Next.Category == "[")
                    {
                        Read("[");
                        indices.Add(ParseExpr());
                        Read("]");
                    }

                    return new Var
                    {
                        ArrayIndex = indices,
                        Id = id
                    };
                }
                case "(":
                {
                    var args = new List<Arg>();

                    Read("(");
                    while (Next.Category != ")")
                    {
                        args.Add(ParseArg());
                        if (Next.Category != ",")
                        {
                            break;
                        }

                        Read(",");
                    }

                    return new Call
                    {
                        Id = id,
                        Args = args
                    };
                }
                default:
                    return new Var
                    {
                        Id = id,
                        ArrayIndex = new List<Expr>()
                    };
            }
        }

        public Arg ParseArg()
        {
            var id = Read("id").Lexeme;
            Read(":");
            var expr = ParseExpr();
            return new Arg
            {
                Id = id,
                Expr = expr
            };
        }
    }
}