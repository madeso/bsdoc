using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Bitsquid;
/// Represents a scope in the in the source document. Scopes are created by indentation, and
/// for that indentation, a particular set of tags that will be applied to objects with that
/// indentation.
public class Scope
{
    public Scope(string indent, ImmutableArray<string> tags)
    {
        this.indent = indent;
        this.tags = tags;
    }

    public string indent { get; set; }
    public ImmutableArray<string> tags { get; init; }
}

/// Represents a transformation rule. The pattern is a regex pattern that the rule matches, and
/// the proc is a block that is run if there is match.
public record Rule(Regex pattern, Action<ParagraphParserEnv, Match> proc);

/// Represents the environment of a running paragraph parser.
public class ParagraphParserEnv
{
	/// The tags that will be used by default for indented text.
	public ImmutableArray<string> indent_tags { get; }

	/// The list of nested scopes that represent the current state.
	public List<Scope> scopes{ get; init; } = new();

	/// The lines of the document that remain to be processed.
	public List<string> lines { get; private set; } = new();

	/// The Generator used to generate the HTML.
	public Generator generator { get; }

    private readonly ImmutableArray<Rule> rules;
    private readonly SpanParser span;

	/// Initializes the environment with a list of rules, a SpanParser for inline transforms and
	/// a Generator for generating the HTML.
    public ParagraphParserEnv(ImmutableArray<Rule> rules, SpanParser span, Generator generator)
	{
		this.rules = rules;
		this.scopes.Add(new Scope("", ImmutableArray<string>.Empty));
		this.indent_tags = ImmutableArray.Create<string>("blockquote");
		this.span = span;
		this.generator = generator;
	}

    private static Regex InitialSpace = new Regex(@"^\s*", RegexOptions.Compiled);
    private static readonly Regex JustSpace = new Regex(@"^\s*$", RegexOptions.Compiled);

    public string LinesShiftChomp()
    {
        var line = lines[0].TrimEnd();
        lines.RemoveAt(0);
        return line;
    }

	/// Parses the text, applies the rules, writing the result to the generator.
	public void parse(string atext)
	{
		this.lines = atext.Split('\n').ToList();

		while(this.lines.Count > 0)
		{
			var line = LinesShiftChomp();

            // Split line into indent and text. Blank lines inherit last indent
            string indent = InitialSpace.Match(line).Value;
            string text = line.Substring(indent.Length);
            if (JustSpace.IsMatch(line))
            {
                indent = scopes.Last()?.indent ?? "";
            }

            process_indent(indent);
			process_text(text);
		}
	}

	/// Writes the line raw (without span-line transformations) to the generator.
	void write_raw(ImmutableArray<string> tags, string? line)
	{
		this.generator.AddWithArray(this.scopes.Last().tags.Concat(tags).Select<string, string?>(x=>x).ToImmutableArray(), line);
	}

	void write_escaped(ImmutableArray<string> tags, string line)
	{
		line = line.Replace("&", "&amp;");
		line = line.Replace("<", "&lt;");
		write_raw(tags, line);
	}

	/// Writes the line (with span-line transformation) to the generator using the tags.
	/// (Scope tags are added automatically.)
    public void write(ImmutableArray<string> tags, string? line)
	{
		if(line != null)
			line = this.span.transform(line);
		write_raw(tags, line);
	}
	

	/// Convenience function for writing some raw text, followed by some span-transformed
	/// text.
	void write_both(ImmutableArray<string>tags, string raw, string line)
	{
		line = raw + this.span.transform(line);
		write_raw(tags, line);
	}

	/// Handles indentation changes. Pops all de-indented scopes and adds new scopes for
	/// indentation.
	void process_indent(string indent)
	{
		if( this.scopes.Last().indent.Length > 0 )
        {
            if (indent.Length == 0) return;

			if(indent.Length > this.scopes[^2].indent.Length)
                this.scopes[^1].indent = indent;
			else
				this.scopes.pop();
		}

		while(indent.Length < this.scopes.Last().indent.Length)
			this.scopes.pop();

		if(indent.Length > this.scopes.Last().indent.Length)
		{
			this.scopes.Add(new Scope(indent, this.scopes.Last().tags.Concat(this.indent_tags).ToImmutableArray()));
		}
	}

	/// Processes text by applying the first rule that matches.
    public void process_text(string line)
	{
		foreach(var rule in this.rules)
		{
			var m = rule.pattern.Match(line);
			if(m.Success)
            {
                rule.proc(this, m);
                return;
            }
		}
	}
}

/// Class for parsing the lines of a source document.
///
/// The parser is created by adding rules. Each rule is a regex that is matched and
/// a command that should be performed on match. The command from the first rule that
/// matches the line is applied.
class ParagraphParser
{
	/// Returns the list of rules
	public List<Rule> rules {get; init;} = new();

	ParagraphParser()
	{
		add_rule(new Regex("^\\s*$/"), ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
    }

	/// Parses the text using the rule set, using the specified SpanParser and Generator.
	/// The result is written to the generator.
	void parse(string text, SpanParser span, Generator generator)
    {
        var env = new ParagraphParserEnv(this.rules.ToImmutableArray(), span, generator);
        env.parse(text);
    }

	/// Adds a generic rule, when the pattern is matched, the block is called. The first
	/// argument to the block is the running ParagraphParserEnv, and the second argument
	/// is the match object from the regex.
	///
	/// This is the main function for defining rules. The result of the functions are
	/// convenience functions.
	void on(Regex pattern, Action<ParagraphParserEnv, Match> action)
	{
		this.rules.Add(new Rule(pattern, action));
	}

	/// Adds a rule for parsing a block of text delimited by a start and end pattern.
	/// When the pattern is matched, the action block is called, with the arguments:
	/// 
	///     (env, block, start_match, end_match)
	///
	/// Env is the running ParagraphParserEnv, block is an Array of the lines delimited
	/// by the patterns. start_match and end_match are the match objects for the start
	/// and end patterns.
	void on_block(Regex start_pattern, Regex end_pattern, Action<ParagraphParserEnv, ImmutableArray<string>, Match, Match> action)
	{
		on(start_pattern, (env, start_match) => {
			Match? end_match = null;
			var indent = env.scopes.Last().indent;
			var block = new List<string>();
			while(true)
			{
				var line = env.LinesShiftChomp();
				line = line[Math.Min(line.Length, indent.Length)..];
                end_match = end_pattern.Match(line);
				if(end_match.Success)
					break;
				block.Add(line);
			}

            action(env, block.ToImmutableArray(), start_match, end_match);
        });
	}

	/// Adds a rule that writes the first matching group of the regex with the specified
	/// context. If before is specified, a null-line with that context is written before.
	void add_rule(Regex pattern, ImmutableArray<string>? before, ImmutableArray<string> context)
	{
		on(pattern, (env, match) => {
			if(before != null) env.write(before.Value, null);
			env.write(context, match.Groups[1].Value);
		});
	}

	/// Adds a list rule. On match a new scope is created, with the specified context.
	void add_list_rule(Regex pattern, ImmutableArray<string>? before, ImmutableArray<string> context)
	{
		on(pattern, (env, match) => {
			if(before != null) env.write(before.Value, null);
			env.scopes.Add(new Scope("", env.scopes.Last().tags.Concat(context).ToImmutableArray()));
			env.process_text(match.Groups[1].Value);
		});
	}

	/// Adds a list rule with a header. A different context (the header_context) is applied
	/// to the matching lines, the (main_context) is used as scope for the following lines.
	void add_list_rule_with_header(Regex pattern, ImmutableArray<string>? before,
		ImmutableArray<string> header_context, ImmutableArray<string> main_context)
	{
		on(pattern, (env, match) => {
			if(before != null) env.write(before.Value, null);
			env.write(header_context, match.Groups[1].Value);
			env.scopes.Add(new Scope("", env.scopes.Last().tags.Concat(main_context).ToImmutableArray()));
		});
	}
}
