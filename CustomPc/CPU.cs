using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static CustomPc.Opcode.ParType;

namespace CustomPc
{
    public static class CPU
    {
        public static readonly Dictionary<string, MethodInfo> Commands = new();
        public static readonly Dictionary<string, Var> Variables = new();
        public static readonly Dictionary<string, Opcode[]> Files = new();
        public static readonly Dictionary<string, int> GotoDict = new();
        public static readonly Dictionary<string, string> WhereGotoDict = new();

        public static readonly string[]
            CmdExcept =
            {
                "def", "jmp", "jlt", "jgt", "jeq", "jne", "stp"
            }; // define, jump, less than, greater than, equal, !equal, step

        public static readonly Stopwatch Stpw = new();
        public static Stack<string> runningFile = new();
        public static Stack<int> runningId = new();
        public static int highId = 0;

        public static void Start()
        {
            if (!Directory.Exists("program code"))
            {
                Directory.CreateDirectory("program code");
                using var sw = File.CreateText("program code/main.pc");
                sw.Write("prt \"Hello World!\"");
                sw.Close();
            }

            foreach (var ms in Assembly.GetExecutingAssembly().GetTypes().Select(t =>
                t.GetMethods().Where(m => m.GetCustomAttributes<OperatorAttribute>().Any())))
                
            foreach (var command in ms)
            {
                var cmdAt = command.GetCustomAttribute<OperatorAttribute>();
                Commands.Add(cmdAt.key, command);
            }

            ReadAndRegisterFile("program code/main.pc");

            Stpw.Start();

            Run("main");
            Exit();
        }

        public static void Run(string name, int line = 0)
        {
            var file = Files[name];
            var thisId = highId++;
            runningFile.Push(name);
            runningId.Push(thisId);
            var i = line;
            try
            {
                for (; i < file.Length; i++) // line interp
                {
                    var op = file[i];
                    if (op.mainOp is "stp") i += (int)op.GetNumber(0) - 1;
                    else if (op.mainOp is "jmp" || op.mainOp is "jlt" && op.GetNumber(1) < op.GetNumber(2) ||
                             op.mainOp is "jgt" && op.GetNumber(1) > op.GetNumber(2) ||
                             op.mainOp is "jeq" && op.Get(1) == op.Get(2) ||
                             op.mainOp is "jne" && op.Get(1) != op.Get(2))
                        if (op[0] is "rtn")
                        {
                            if (runningId.Count > 1) runningId.Pop();
                            break;
                        }
                        else if (op.pars[0] is Opcode.ParType.String) Run(runningFile.Peek(), GotoDict[op[0]]);
                        else i = (int)op.GetNumber(0) - 2;
                    else if (op.isNoOp) continue;
                    else if (Commands.ContainsKey(op.mainOp)) Commands[op.mainOp].Invoke(null, new[] { op });

                    if (runningId.Peek() != thisId) break; // break for
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Err on line `{i + 1}` in file `{name}`:\n{e}");
                Exit();
                Environment.Exit(0);
            }

            runningFile.Pop();
        }

        public static void ReadAndRegisterFile(string dir)
        {
            var file = dir.Replace("\\", "/").Split('/')[^1].Split('.')[0];
            Files.Add(file,
                ReadFile(dir).Split(Environment.NewLine).Select((s, i) => new Opcode(file, s, i)).ToArray());
        }

        public static string ReadFile(string dir)
        {
            using StreamReader sr = new(dir);
            var data = sr.ReadToEnd();
            sr.Close();
            return data;
        }

        public static void Exit()
        {
            Stpw.Stop();
            Console.WriteLine($"Program Ended\nProgram Ran for: [{Stpw.Elapsed}]\nPress Any Key To Continue");
            Console.ReadKey();
        }
    }

    public class Opcode
    {
        private static Regex _varX = new(@"^\[(.*?)\]");
        private static Regex _strX = new(@"^""(.*?)""");
        private static Regex _numX = new(@"^((?:[\d\.\-e])*)");
        private static Regex _startSpace = new(@"^[ \t]*");

        public enum ParType
        {
            Variable,
            Number,
            String
        }

        public string mainOp;
        public ParType[] pars;
        public string[] data;
        public bool isNoOp;

        public Opcode(string file, string data, int line)
        {
            data = _startSpace.Replace(data, "");
            if (data == "")
            {
                isNoOp = true;
                return;
            }

            if (!data.Contains(' '))
            {
                mainOp = data;
                if (!CPU.Commands.ContainsKey(data))
                {
                    isNoOp = true;
                    return;
                }

                this.pars = Array.Empty<ParType>();
                this.data = Array.Empty<string>();
                return;
            }

            var firstSpace = data.IndexOf(' ');
            var start = data[..firstSpace];
            var compData = data[(firstSpace + 1)..];
            if (!CPU.Commands.ContainsKey(start) && !CPU.CmdExcept.Contains(start))
            {
                isNoOp = true;
                return;
            }

            mainOp = start;

            List<ParType> pars = new();
            List<string> datas = new();
            while (compData.Any())
            {
                compData = _startSpace.Replace(compData, "");
                var p = GetType(compData, out var split);
                if (split == "") break;
                var ind = compData.IndexOf(split, StringComparison.Ordinal) + split.Length + 1;
                pars.Add(p);
                datas.Add(split);
                if (split == compData) break;
                compData = compData[ind..];
            }

            this.pars = pars.ToArray();
            this.data = datas.ToArray();
            if (CPU.CmdExcept.Contains(mainOp)) isNoOp = true;
            if (mainOp != "def") return;
            CPU.GotoDict.Add(this[0], line + 1);
            CPU.WhereGotoDict.Add(this[0], file);
        }

        public static ParType GetType(string s, out string split)
        {
            ParType Match(Regex r, ParType p, out string split)
            {
                if (r.IsMatch(s))
                {
                    split = r.Match(s).Groups[1].Value;
                    return p;
                }

                split = "";
                return ParType.String;
            }

            var p = Match(_varX, Variable, out split);
            if (split is "") p = Match(_numX, Number, out split);
            if (split is "") p = Match(_strX, ParType.String, out split);
            return p;
        }

        public string GetString(int indx)
        {
            if (pars.Length <= indx) return "nul";
            var d = data[indx];
            return pars[indx] switch
            {
                Variable => CPU.Variables[d].Val(),
                ParType.String or Number => d,
                _ => "nul"
            };
        }

        public double GetNumber(int indx)
        {
            if (pars.Length <= indx) return double.NaN;
            var d = data[indx];
            return pars[indx] switch
            {
                Variable => CPU.Variables[d] is Num n
                    ? n.value
                    : double.TryParse(CPU.Variables[d].Val(), out var i)
                        ? i
                        : double.NaN,
                ParType.String or Number => double.TryParse(d, out var i) ? i : double.NaN,
                _ => double.NaN
            };
        }

        public string Get(int i) => pars[i] is Variable ? CPU.Variables[this[i]].Val() : this[i];
        public string this[int i] => data[i];
    }

    public abstract class Var
    {
        public abstract string Val();
    }

    public class Num : Var
    {
        public double value;

        public override string Val()
        {
            var log = (long)Math.Log10(value);
            return value < 2e9 ? $"{value:###,##0.##}" : $"{value / Math.Pow(10, log):0.00}e{log}";
        }
    }

    public class Str : Var
    {
        public string value;
        public override string Val() => value;
    }
}