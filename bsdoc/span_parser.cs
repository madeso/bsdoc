using System.Text.RegularExpressions;

namespace Bitsquid;

record SpanParserRule(Regex pattern, string? replace, Func<string, string> rep);

/// Handles in-line transformation of text.
///
/// The transformation consist of a number of RegEx rules that are applied in turn to the text.
public class SpanParser
{
	private readonly List<SpanParserRule> _rules = new();

	/// Adds a new rule. A rule consists of a match pattern and a replacement string, or a
	/// block that returns the replacement value. The block gets the regex match object as
	/// argument.
	public void add_rule(Regex pattern, string with)
	{
		this._rules.Add(new SpanParserRule(pattern, with, _ => string.Empty));
	}

    public void add_rule(Regex pattern, Func<string, string> rep)
    {
        this._rules.Add(new SpanParserRule(pattern, null, rep));
    }

    /// Transforms the text using the set of rules in this span parser and returns the
    /// result.
    public string transform(string text)
	{
		foreach(var rule in this._rules)
		{
			text = rule.replace != null
                ? rule.pattern.Replace(text, rule.replace)
                : rule.pattern.Replace(text, (rep) => rule.rep(rep.Value));
		}

        return text;
    }
}