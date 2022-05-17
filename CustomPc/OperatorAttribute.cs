using System;

namespace CustomPc
{
    [AttributeUsage(AttributeTargets.Method)]
    public class OperatorAttribute : Attribute
    {
        public string key;

        public OperatorAttribute(string key) => this.key = key;
    }
}