using System.Globalization;

namespace MushTown.Shared;

// The number formulas of the WZ skill nodes (common/*). A plain number is a formula too.
// Grammar: + - * / (and a leading minus), brackets, x = the skill level, d(a) = round down,
// u(a) = round up, logN(a) = the logarithm of a to base N. Any other writing throws, so an
// unseen formula is never read as a wrong number.
public static class SkillFormula
{
    public static double Eval(string formula, int level)
    {
        var parser = new Parser(formula.Replace(" ", ""), level);
        double value = parser.Sum();
        if (!parser.AtEnd) throw new InvalidDataException($"bad formula: {formula}");
        return value;
    }

    // The value of every level 1..maxLevel (index 0 is level 1).
    public static double[] Levels(string formula, int maxLevel) =>
        Enumerable.Range(1, maxLevel).Select(lv => Eval(formula, lv)).ToArray();

    private sealed class Parser
    {
        private readonly string _s;
        private readonly int _level;
        private int _i;

        public Parser(string s, int level) { _s = s; _level = level; }

        public bool AtEnd => _i == _s.Length;

        private char Peek => _i < _s.Length ? _s[_i] : '\0';

        public double Sum()
        {
            double v = Product();
            while (Peek is '+' or '-')
            {
                char op = _s[_i++];
                double r = Product();
                v = op == '+' ? v + r : v - r;
            }
            return v;
        }

        private double Product()
        {
            double v = Unit();
            while (Peek is '*' or '/')
            {
                char op = _s[_i++];
                double r = Unit();
                v = op == '*' ? v * r : v / r;
            }
            return v;
        }

        private double Unit()
        {
            int start = _i;
            if (Peek == '-')
            {
                _i++;
                return -Unit();
            }
            if (Peek == '(')
            {
                _i++;
                double inner = Sum();
                Close();
                return inner;
            }
            if (char.IsDigit(Peek))
            {
                while (char.IsDigit(Peek) || Peek == '.') _i++;
                return double.Parse(_s[start.._i], CultureInfo.InvariantCulture);
            }
            while (char.IsLetterOrDigit(Peek)) _i++;
            string name = _s[start.._i];
            if (name == "x") return _level;
            if (Peek != '(') throw new InvalidDataException($"bad formula: {_s}");
            _i++;
            double arg = Sum();
            Close();
            if (name == "u") return Math.Ceiling(arg);
            if (name == "d") return Math.Floor(arg);
            if (name.StartsWith("log") && double.TryParse(name[3..], NumberStyles.Float, CultureInfo.InvariantCulture, out double b))
                return Math.Log(arg) / Math.Log(b);
            throw new InvalidDataException($"bad formula: {_s}");
        }

        private void Close()
        {
            if (Peek != ')') throw new InvalidDataException($"bad formula: {_s}");
            _i++;
        }
    }
}
