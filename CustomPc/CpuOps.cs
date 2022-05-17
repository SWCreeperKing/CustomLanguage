using System;
using System.Text;
using static CustomPc.CPU;

namespace CustomPc
{
    public static class CpuOps
    {
        [Operator("prt")]
        public static void Print(Opcode op)
        {
            Stpw.Stop();
            StringBuilder sb = new("> ");
            for (var i = 0; i < op.data.Length; i++) sb.Append(op.GetString(i).Replace("\\n", "\n> "));
            Console.WriteLine(sb);
            Stpw.Start();
        }

        [Operator("inp")]
        public static void Input(Opcode op)
        {
            Stpw.Stop();
            Console.Write("< ");
            SetVariable(op[0], Console.ReadLine()!);
            Stpw.Start();
        }

        [Operator("var")] public static void Set(Opcode op) => SetVariable(op[0], op.Get(1));
        [Operator("add")] public static void Add(Opcode op) => SetVariable(op[0], op.GetNumber(1) + op.GetNumber(2));
        [Operator("sub")] public static void Sub(Opcode op) => SetVariable(op[0], op.GetNumber(1) - op.GetNumber(2));
        [Operator("mlt")] public static void Multi(Opcode op) => SetVariable(op[0], op.GetNumber(1) * op.GetNumber(2));
        [Operator("div")] public static void Divide(Opcode op) => SetVariable(op[0], op.GetNumber(1) / op.GetNumber(2));
        [Operator("req")] public static void LoadProgramFile(Opcode op) => ReadAndRegisterFile($"program code/{op.GetString(0)}.pc");
        [Operator("lod")] public static void LoadDataFile(Opcode op) => SetVariable(op[0], op.Get(1));
        [Operator("cal")] public static void Goto(Opcode op) => Run(WhereGotoDict[op[0]], GotoDict[op[0]]);

        [Operator("end")]
        public static void End(Opcode op)
        {
            Exit();
            Environment.Exit(0);
        }

        [Operator("inc")]
        public static void Increment(Opcode op) => SetVariable(op[0], op.GetNumber(0) + op.GetNumber(1));

        [Operator("rtn")]
        public static void Return(Opcode op)
        {
            if (runningId.Count > 1) runningId.Pop();
            else
            {
                Exit();
                Environment.Exit(0);
            }
        }

        public static void SetVariable(string key, double value)
        {
            if (Variables.ContainsKey(key))
                if (Variables[key] is Num n) n.value = value;
                else Variables[key] = new Num { value = value };
            else Variables.Add(key, new Num { value = value });
        }

        public static void SetVariable(string key, string value)
        {
            if (Variables.ContainsKey(key))
                if (Variables[key] is Str n) n.value = value;
                else Variables[key] = new Str { value = value };
            else Variables.Add(key, new Str { value = value });
        }
    }
}