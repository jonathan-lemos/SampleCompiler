using System;
using System.Collections.Generic;
using System.Linq;

// below disable certain resharper warnings

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace SampleSemanalyzer
{
    public static class VarTab
    {
        public static readonly IList<IDictionary<string, TypeSpec>> Scopes = new List<IDictionary<string, TypeSpec>>();

        public static IList<IDictionary<string, TypeSpec>> Clear()
        {
            Scopes.Clear();
            PushScope();

            Add("print", new TypeSpec
            {
                Base = new Function
                {
                    ReturnType = TypeSpec.OfPrimitive("none"),
                    Types = new HashSet<Param>
                    {
                        new Param
                        {
                            Id = "x",
                            Type = TypeSpec.OfPrimitive("ANY")
                        }
                    }
                }
            });

            Add("readInt", new TypeSpec
            {
                Base = new Function
                {
                    ReturnType = TypeSpec.OfPrimitive("int"),
                    Types = new HashSet<Param>()
                }
            });

            Add("readFloat", new TypeSpec
            {
                Base = new Function
                {
                    ReturnType = TypeSpec.OfPrimitive("float"),
                    Types = new HashSet<Param>()
                }
            });

            Add("readBool", new TypeSpec
            {
                Base = new Function
                {
                    ReturnType = TypeSpec.OfPrimitive("bool"),
                    Types = new HashSet<Param>()
                }
            });

            return Scopes;
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
            VarTab.Clear();
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
            VarTab.PushScope();
            IfBlock.ForEach(stmt => stmt.Verify());
            VarTab.PopScope();
            VarTab.PushScope();
            ElseBlock.ForEach(stmt => stmt.Verify());
            VarTab.PopScope();
        }
    }

    public class WhileStmt : Stmt
    {
        public Condition Condition { get; set; }
        public IList<Stmt> Statements { get; set; }

        public void Verify()
        {
            Condition.Verify();
            VarTab.PushScope();
            Statements.ForEach(stmt => stmt.Verify());
            VarTab.PopScope();
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

    public class FundecStmt : Stmt
    {
        public string Id { get; set; }
        public ISet<Param> Params { get; set; }
        public IList<Stmt> Stmts { get; set; }
        public TypeSpec ReturnType { get; set; }

        public void Verify()
        {
            FuncState.PushFunc(ReturnType);
            VarTab.PushScope();
            Params.ForEach(par => VarTab.Add(par.Id, par.Type));
            Stmts.ForEach(stmt => stmt.Verify());
            VarTab.PopScope();
            FuncState.PopFunc();

            VarTab.Add(Id, new TypeSpec
            {
                Base = new Function
                {
                    Types = Params,
                    ReturnType = ReturnType
                }
            });
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

    public class TypeSpec : IEquatable<TypeSpec>
    {
        public TypeBase Base { get; set; }
        public int ArrayCount { get; set; }

        public static TypeSpec OfPrimitive(string prim) => new TypeSpec
        {
            ArrayCount = 0,
            Base = new Primitive
            {
                Type = prim
            }
        };

        public bool IsArithmetic => ArrayCount == 0 && Base is Primitive prim &&
                                    new HashSet<string> {"int", "float", "bool"}.Contains(prim.Type);

        public static bool operator ==(TypeSpec t1, TypeSpec t2)
        {
            if (ReferenceEquals(t1, t2)) return true;
            if (ReferenceEquals(t1, null)) return false;
            // to shut up resharper
            if (ReferenceEquals(t2, null)) return false;

            if (t1.ArrayCount == 0 && t1.Base is Primitive x1 && x1.Type == "ANY" ||
                t2.ArrayCount == 0 && t2.Base is Primitive x2 && x2.Type == "ANY") return true;

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

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypeSpec) obj);
        }

        public bool Equals(TypeSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Base, other.Base) && ArrayCount == other.ArrayCount;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Base != null ? Base.GetHashCode() : 0) * 397) ^ ArrayCount;
            }
        }
    }

    public interface TypeBase
    {
    }

    public class Primitive : TypeBase, IEquatable<Primitive>
    {
        public string Type { get; set; }

        public bool Equals(Primitive other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Primitive) obj);
        }

        public override int GetHashCode()
        {
            return (Type != null ? Type.GetHashCode() : 0);
        }
    }

    public class Function : TypeBase, IEquatable<Function>
    {
        public TypeSpec ReturnType { get; set; }
        public ISet<Param> Types { get; set; }

        public bool Equals(Function other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ReturnType, other.ReturnType) && Equals(Types, other.Types);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Function) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ReturnType != null ? ReturnType.GetHashCode() : 0) * 397) ^ (Types != null ? Types.GetHashCode() : 0);
            }
        }
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

            if (nextType != null && !termType.IsArithmetic)
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
        public ISet<Param> Params { get; set; }
        public TypeSpec ReturnType { get; set; }
        public IList<Stmt> Stmts { get; set; }

        public TypeSpec Verify()
        {
            FuncState.PushFunc(ReturnType);
            VarTab.PushScope();
            Params.ForEach(par => VarTab.Add(par.Id, par.Type));
            Stmts.ForEach(stmt => stmt.Verify());
            VarTab.PopScope();
            FuncState.PopFunc();

            Params.ForEach(par => par.Verify());
            if (Params.Select(x => x.Id).ToList().Count != Params.Select(x => x.Id).ToHashSet().Count)
            {
                throw new ArgumentException();
            }

            return new TypeSpec
            {
                Base = new Function
                {
                    ReturnType = ReturnType,
                    Types = Params
                }
            };
        }
    }

    public class Term
    {
        public Factor Factor { get; set; }
        public string? Mulop { get; set; }
        public Term? Next { get; set; }

        public TypeSpec Verify()
        {
            var type1 = Factor.Verify();
            var type2 = Next?.Verify();

            if (type2 != null && type1 != type2)
            {
                throw new ArgumentException();
            }

            if (type2 != null && !type1.IsArithmetic)
            {
                throw new ArgumentException();
            }

            return type1;
        }
    }

    public interface Factor
    {
        TypeSpec Verify();
    }

    public class Var : Factor
    {
        public string Id { get; set; }
        public IList<Expr> ArrayIndex { get; set; }

        public TypeSpec Verify()
        {
            var baseType = VarTab.Get(Id);
            if (baseType.ArrayCount < ArrayIndex.Count)
            {
                throw new ArgumentException();
            }

            return new TypeSpec
            {
                ArrayCount = baseType.ArrayCount - ArrayIndex.Count,
                Base = baseType.Base
            };
        }
    }

    public class Call : Stmt, Factor
    {
        public string Id { get; set; }
        public IList<Arg> Args { get; set; }

        public TypeSpec Verify()
        {
            var baseType = VarTab.Get(Id);
            if (baseType.ArrayCount > 0 || !(baseType.Base is Function func))
            {
                throw new ArgumentException();
            }

            var argIds = Args.Select(x => x.Id).ToHashSet();
            var parIds = func.Types.Select(x => x.Id).ToHashSet();

            if (!parIds.IsSupersetOf(argIds))
            {
                throw new ArgumentException();
            }

            if (!func.Types.Where(par => par.Default != null).Select(par => par.Id).ToHashSet()
                .IsSupersetOf(parIds.Except(argIds)))
            {
                throw new ArgumentException();
            }

            (from arg in Args
                join par in func.Types on arg.Id equals par.Id
                select (arg, par)).ForEach(tup =>
            {
                var (arg, par) = tup;
                if (arg.Expr.Verify() != par.Type)
                {
                    throw new ArgumentException();
                }
            });

            return func.ReturnType;
        }

        void Stmt.Verify()
        {
            Verify();
        }
    }

    public class Num : Factor
    {
        public int Number { get; set; }

        public TypeSpec Verify() => new TypeSpec
        {
            Base = new Primitive
            {
                Type = "int"
            }
        };
    }

    public class Float : Factor
    {
        public double Number { get; set; }

        public TypeSpec Verify() => new TypeSpec
        {
            Base = new Primitive
            {
                Type = "int"
            }
        };
    }

    public class ParenFactor : Factor
    {
        public Expr Expr { get; set; }

        public TypeSpec Verify() => Expr.Verify();
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

        private Token Next
        {
            get
            {
                try
                {
                    return _tokens.Peek();
                }
                catch (Exception)
                {
                    throw new ArgumentException();
                }
            }
        }

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
            "fun" => ParseFundecStmt(),
            "id" => ParseIdStmt(),
            _ => throw new ArgumentException(Next.ToString())
        };

        public IfStmt ParseIfStmt()
        {
            Read("if");
            var tmp = ParseCondition();
            if (!(tmp is Condition cond))
            {
                throw new ArgumentException();
            }

            Read("then");
            var stmt = ParseStmtList();
            if (Next.Category == "else")
            {
                Read("else");
                var elseStmt = ParseStmtList();
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
            if (!(ParseCondition() is Condition cond))
            {
                throw new ArgumentException();
            }

            Read("do");
            var stmt = ParseStmtList();
            Read("done");
            return new WhileStmt
            {
                Condition = cond,
                Statements = stmt
            };
        }

        public FundecStmt ParseFundecStmt()
        {
            Read("fun");
            var id = Read("id").Lexeme;
            Read("(");
            var pars = ParseParams();
            Read(")");
            Read(":");
            var type = ParseTypeSpec();
            Read("<-");
            Read("begin");
            var stmts = ParseStmtList();
            Read("end");
            return new FundecStmt
            {
                Id = id,
                Params = pars,
                ReturnType = type,
                Stmts = stmts
            };
        }

        public Stmt ParseIdStmt()
        {
            var id = Read("id").Lexeme;
            if (Next.Category == "<-")
            {
                Read("<-");
                var expr = ParseExpr();
                Read(";");
                return new AssgnStmt
                {
                    Id = id,
                    Expr = expr
                };
            }

            Read("(");
            var args = new List<Arg>();
            while (true)
            {
                try
                {
                    args.Add(ParseArg());
                    if (Next.Category != ",")
                    {
                        break;
                    }

                    Read(",");
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            Read(")");
            Read(";");
            return new Call
            {
                Id = id,
                Args = args
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
            Read(";");
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
                    var types = ParseParams();
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
                Stmts = stmts
            };
        }

        public ISet<Param> ParseParams()
        {
            var ret = new HashSet<Param>();
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
            if (Next.Category != "relop")
            {
                return addExpr;
            }

            var relop = Read("relop").Lexeme;
            var addExpr2 = ParseAddExpr();

            if (Next.Category != "binop")
            {
                return new Condition
                {
                    Left = addExpr,
                    Relop = relop,
                    Right = addExpr2
                };
            }

            var binop = Read("binop").Lexeme;
            if (!(ParseCondition() is Condition cond2))
            {
                throw new ArgumentException();
            }

            return new Condition
            {
                Left = addExpr,
                Relop = relop,
                Right = addExpr2,
                Binop = binop,
                Next = cond2
            };
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
            if (Next.Category == "mulop")
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
                    return new Num {Number = int.Parse(Read("num").Lexeme)};
                case "float":
                    return new Float {Number = double.Parse(Read("float").Lexeme)};
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

                    Read(")");

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