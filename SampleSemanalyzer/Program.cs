﻿using System;

namespace SampleSemanalyzer
{
    /*
     * start       -> stmt-list
     * stmt-list   -> stmt stmt-list | stmt
     * stmt        -> if-stmt | while-stmt | return-stmt | vardec-stmt | fundec-stmt
     * if-stmt     -> if condition then stmt-list fi | if condition then stmt-list else stmt-list fi
     * while-stmt  -> while condition do stmt done
     * assgn-stmt  -> id <- expr ;
     * return-stmt -> return expr ;
     * vardec-stmt -> let id : type-spec <- expr
     * type-spec   -> type-base | [ type-spec ]
     * type-base   -> primitive | ( types ) : type-spec
     * array-spec  -> [ ] array-spec | empty
     * types       -> type-list | empty
     * type-list   -> type-spec , types | type-spec
     * expr        -> array-expr | add-expr | condition | func-expr
     * condition   -> add-expr relop add-expr | add-expr relop add-expr binop condition
     * add-expr    -> term addop add-expr | term
     * array-expr  -> [ exprs ]
     * exprs       -> expr-list | empty
     * expr-list   -> expr , expr-list | expr
     * func-expr   -> ( params ) : type-spec -> { stmt-list }
     * params      -> param-list | empty
     * param-list  -> param , param-list | param
     * param       -> id : type | id : type <- expr
     * term        -> factor mulop term | factor
     * factor      -> var | call | num | float | ( expr )
     * var         -> id | var [ expr ]
     * call        -> id ( args )
     * args        -> arg-list | empty
     * arg-list    -> id : expr , arg-list | id : expr
     */

    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}