﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CommonMark.Parser
{
    /// <summary>
    /// Contains the regular expressions that are used in the parsers.
    /// </summary>
    internal static partial class Scanner
    {
        private const RegexOptions useCompilation = RegexOptions.None;

        /// <summary>
        /// List of valid schemes of an URL. The array must be sorted.
        /// </summary>
        private static readonly string[] schemeArray = new[] { "aaa", "aaas", "about", "acap", "adiumxtra", "afp", "afs", "aim", "apt", "attachment", "aw", "beshare", "bitcoin", "bolo", "callto", "cap", "chrome", "chrome-extension", "cid", "coap", "com-eventbrite-attendee", "content", "crid", "cvs", "data", "dav", "dict", "dlna-playcontainer", "dlna-playsingle", "dns", "doi", "dtn", "dvb", "ed2k", "facetime", "feed", "file", "finger", "fish", "ftp", "geo", "gg", "git", "gizmoproject", "go", "gopher", "gtalk", "h323", "hcp", "http", "https", "iax", "icap", "icon", "im", "imap", "info", "ipn", "ipp", "irc", "irc6", "ircs", "iris", "iris.beep", "iris.lwz", "iris.xpc", "iris.xpcs", "itms", "jar", "javascript", "jms", "keyparc", "lastfm", "ldap", "ldaps", "magnet", "mailto", "maps", "market", "message", "mid", "mms", "ms-help", "msnim", "msrp", "msrps", "mtqp", "mumble", "mupdate", "mvn", "news", "nfs", "ni", "nih", "nntp", "notes", "oid", "opaquelocktoken", "palm", "paparazzi", "platform", "pop", "pres", "proxy", "psyc", "query", "res", "resource", "rmi", "rsync", "rtmp", "rtsp", "secondlife", "service", "session", "sftp", "sgn", "shttp", "sieve", "sip", "sips", "skype", "smb", "sms", "snmp", "soap.beep", "soap.beeps", "soldat", "spotify", "ssh", "steam", "svn", "tag", "teamspeak", "tel", "telnet", "tftp", "things", "thismessage", "tip", "tn3270", "tv", "udp", "unreal", "urn", "ut2004", "vemmi", "ventrilo", "view-source", "webcal", "ws", "wss", "wtai", "wyciwyg", "xcon", "xcon-userid", "xfire", "xmlrpc.beep", "xmlrpc.beeps", "xmpp", "xri", "ymsgr", "z39.50r", "z39.50s" };
        private static readonly string[] blockTagNames = new[] { "article", "aside", "blockquote", "body", "button", "canvas", "caption", "col", "colgroup", "dd", "div", "dl", "dt", "embed", "fieldset", "figcaption", "figure", "footer", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hgroup", "hr", "iframe", "li", "map", "object", "ol", "output", "p", "pre", "progress", "script", "section", "style", "table", "tbody", "td", "textarea", "tfoot", "th", "thead", "tr", "ul", "video" };


        private static readonly Regex autolink_email = new Regex("^[a-zA-Z0-9.!#$%&'\\*+/=?^_`{|}~-]+[@][a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?([.][a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*[>]", useCompilation);
        private static readonly Regex close_code_fence = new Regex(@"^([`]{3,}|[~]{3,})(?:\s*)$", useCompilation);

        private static int MatchRegex(string s, int pos, params Regex[] regexes)
        {
            Match m;
            foreach (var r in regexes)
            {
                m = r.Match(s, pos, s.Length - pos);
                if (m.Success && m.Index == pos)
                    return m.Length;
            }

            return 0;
        }

        /// <summary>
        /// Try to match URI autolink after first &lt;, returning number of chars matched.
        /// </summary>
        public static int scan_autolink_uri(string s, int pos)
        {
            /*!re2c
              scheme [:]([^\x00-\x20<>\\]|escaped_char)*[>]  { return (p - start); }
              .? { return 0; }
            */
            // for now the tests do not include anything that would require the use of `escaped_char` part so it is ignored.

            // 24 is the maximum length of a valid scheme
            var checkLen = s.Length - pos;
            if (checkLen > 24)
                checkLen = 24;

            // PERF: potential small improvement - instead of using IndexOf, check char-by-char and return as soon as an invalid character is found ([^a-z0-9\.])
            // alternative approach (if we want to go crazy about performance - store the valid schemes as a prefix tree and lookup the valid scheme char by char and
            // return as soon as the part does not match any prefix.
            var colonpos = s.IndexOf(':', pos, checkLen);
            if (colonpos == -1)
                return 0;

            var potentialScheme = s.Substring(pos, colonpos - pos).ToLowerInvariant();
            if (Array.BinarySearch(schemeArray, potentialScheme) < -1)
                return 0;

            char c;
            for (var i = colonpos + 1; i < s.Length; i++)
            {
                c = s[i];
                if (c == '>')
                    return i - pos + 1;

                if (c == '<' || c <= 0x20)
                    return 0;
            }

            return 0;
        }

        /// <summary>
        /// Try to match email autolink after first &lt;, returning num of chars matched.
        /// </summary>
        public static int scan_autolink_email(string s, int pos)
        {
            /*!re2c
              [a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+
                [@]
                [a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?
                ([.][a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*
                [>] { return (p - start); }
              .? { return 0; }
            */
            return MatchRegex(s, pos, autolink_email);
        }

        /// <summary>
        /// Try to match an HTML block tag including first &lt;.
        /// </summary>
        public static bool scan_html_block_tag(string s, int pos)
        {
            /*!re2c
              [<] [/] blocktagname (spacechar | [>])  { return (p - start); }
              [<] blocktagname (spacechar | [/>]) { return (p - start); }
              [<] [!?] { return (p - start); }
              .? { return 0; }
            */

            if (pos + 1 >= s.Length)
                return false;

            if (s[pos] != '<')
                return false;

            var i = pos + 1;
            var nextChar = s[i];
            if (nextChar == '!' || nextChar == '?')
                return true;

            var slashAtBeginning = nextChar == '/';
            if (slashAtBeginning)
                nextChar = s[++i];

            var j = 0;
            var tagname = new char[10];
            while (char.IsLetter(nextChar) && j <= 10 && ++i < s.Length)
            {
                tagname[j++] = nextChar;
                nextChar = s[i];
            }

            var scheme = new string(tagname, 0, j).ToLowerInvariant();
            if (Array.BinarySearch(blockTagNames, scheme) < 0)
                return false;

            return nextChar == '>' || (!slashAtBeginning && nextChar == '/') || char.IsWhiteSpace(nextChar);
        }

        /// <summary>
        /// Try to match a URL in a link or reference, return number of chars matched.
        /// This may optionally be contained in &lt;..&gt;; otherwise
        /// whitespace and unbalanced right parentheses aren't allowed.
        /// Newlines aren't ever allowed.
        /// </summary>
        public static int scan_link_url(string s, int pos)
        {
            /*!re2c
              [ \n]* [<] ([^<>\n\\\x00] | escaped_char | [\\])* [>] { return (p - start); }
              [ \n]* (reg_char+ | escaped_char | in_parens_nosp)* { return (p - start); }
              .? { return 0; }
            */

            if (pos + 1 >= s.Length)
                return 0;

            var i = pos;
            var c = s[i];
            var nextEscaped = false;
            var lastPos = s.Length - 1;
            // move past any whitespaces
            ScannerCharacterMatcher.MatchWhitespaces(s, ref c, ref i, lastPos);

            if (c == '<')
            {
                if (i == lastPos) return 0;
                c = s[++i];
                while (i <= lastPos)
                {
                    if (c == '\n') return 0;
                    if (c == '<' && !nextEscaped) return 0;
                    if (c == '>' && !nextEscaped) return i - pos + 1;
                    if (i == lastPos) return 0;
                    nextEscaped = !nextEscaped && c == '\\';
                    c = s[++i];
                }
                return 0;
            }

            bool openParens = false;
            while (i <= lastPos)
            {
                if (c == '(' && !nextEscaped)
                {
                    if (openParens)
                        return 0;
                    openParens = true;
                }
                if (c == ')' && !nextEscaped)
                {
                    if (!openParens)
                        return i - pos;
                    openParens = false;
                }
                if (c <= 0x20)
                    return openParens ? 0 : i - pos;
                
                if (i == lastPos)
                    return openParens ? 0 : i - pos + 1;

                nextEscaped = !nextEscaped && c == '\\';
                c = s[++i];
            }

            return 0;
        }

        /// <summary>
        /// Try to match a link title (in single quotes, in double quotes, or
        /// in parentheses), returning number of chars matched.  Allow one
        /// level of internal nesting (quotes within quotes).
        /// </summary>
        public static int scan_link_title(string s, int pos)
        {
            /*!re2c
              ["] (escaped_char|[^"\x00])* ["]   { return (p - start); }
              ['] (escaped_char|[^'\x00])* ['] { return (p - start); }
              [(] (escaped_char|[^)\x00])* [)]  { return (p - start); }
              .? { return 0; }
            */

            if (pos + 2 >= s.Length)
                return 0;

            var c1 = s[pos];
            if (c1 != '"' && c1 != '\'' && c1 != '(')
                return 0;

            if (c1 == '(') c1 = ')';

            char c;
            bool nextEscaped = false;
            for (var i = pos + 1; i < s.Length; i++)
            {
                c = s[i];
                if (c == c1 && !nextEscaped)
                    return i - pos + 1;

                nextEscaped = !nextEscaped && c == '\\';
            }

            return 0;
        }

        /// <summary>
        /// Match space characters, including newlines.
        /// </summary>
        public static int scan_spacechars(string s, int pos)
        {
            /*!re2c
              [ \t\n]* { return (p - start); }
              . { return 0; }
            */
            if (pos >= s.Length)
                return 0;

            for (var i = pos; i < s.Length; i++)
            {
                if (!char.IsWhiteSpace(s[i]))
                    return i - pos;
            }

            return s.Length - pos;
        }
        
        /// <summary>
        /// Match ATX header start.
        /// </summary>
        public static int scan_atx_header_start(string s, int pos, out int headerLevel)
        {
            /*!re2c
              [#]{1,6} ([ ]+|[\n])  { return (p - start); }
              .? { return 0; }
            */

            headerLevel = 1;
            if (pos + 1 >= s.Length)
                return 0;

            if (s[pos] != '#')
                return 0;

            bool spaceExists = false;
            char c;
            for (var i = pos + 1; i < s.Length; i++ )
            {
                c = s[i];
                
                if (c == '#')
                {
                    if (headerLevel == 6)
                        return 0;

                    if (spaceExists)
                        return i - pos;
                    else
                        headerLevel++;
                }
                else if (c == ' ')
                {
                    spaceExists = true;
                }
                else if (c == '\n')
                {
                    return i - pos + 1;
                }
                else
                {
                    return spaceExists ? i - pos : 0;                        
                }
            }

            if (spaceExists)
                return s.Length - pos;
            
            return 0;
        }

        /// <summary>
        /// Match sexext header line.  Return 1 for level-1 header,
        /// 2 for level-2, 0 for no match.
        /// </summary>
        public static int scan_setext_header_line(string s, int pos)
        {
            /*!re2c
              [=]+ [ ]* [\n] { return 1; }
              [-]+ [ ]* [\n] { return 2; }
              .? { return 0; }
            */

            if (pos >= s.Length)
                return 0;

            var c1 = s[pos];

            if (c1 != '=' && c1 != '-')
                return 0;

            char c;
            var fin = false;
            for (var i = pos + 1; i < s.Length; i++ )
            {
                c = s[i];
                if (c == c1 && !fin)
                    continue;

                fin = true;
                if (c == ' ')
                    continue;

                if (c == '\n')
                    break;

                return 0;
            }

            return c1 == '=' ? 1 : 2;
        }

        /// <summary>
        /// Scan a horizontal rule line: "...three or more hyphens, asterisks,
        /// or underscores on a line by themselves. If you wish, you may use
        /// spaces between the hyphens or asterisks."
        /// </summary>
        public static int scan_hrule(string s, int pos)
        {
            // @"^([\*][ ]*){3,}[\s]*$",
            // @"^([_][ ]*){3,}[\s]*$",
            // @"^([-][ ]*){3,}[\s]*$",

            int count = 0;
            char c;
            char x = '\0';
            var ipos = pos;
            while (ipos < s.Length)
            {
                c = s[ipos++];

                if (c == ' ' || c == '\n')
                    continue;
                if (count == 0)
                {
                    if (c == '*' || c == '_' || c == '-')
                        x = c;
                    else
                        return 0;
                    
                    count = 1;
                }
                else if (c == x)
                    count ++;
                else
                    return 0;
            }

            if (count < 3)
                return 0;

            return s.Length - pos;
        }

        /// <summary>
        /// Scan an opening code fence. Returns the number of characters forming the fence.
        /// </summary>
        public static int scan_open_code_fence(string s, int pos)
        {
            /*!re2c
              [`]{3,} / [^`\n\x00]*[\n] { return (p - start); }
              [~]{3,} / [^~\n\x00]*[\n] { return (p - start); }
              .?                        { return 0; }
            */

            if (pos + 3 >= s.Length)
                return 0;

            var fchar = s[pos];
            if (fchar != '`' && fchar != '~')
                return 0;

            var cnt = 1;
            var fenceDone = false;
            char c;
            for (var i = pos + 1; i < s.Length; i++)
            {
                c = s[i];

                if (c == fchar)
                {
                    if (fenceDone)
                        return 0;

                    cnt++;
                    continue;
                }

                fenceDone = true;
                if (cnt < 3)
                    return 0;

                if (c == '\n')
                    return cnt;
            }

            if (cnt < 3)
                return 0;

            return cnt;
        }

        /// <summary>
        /// Scan a closing code fence with length at least len.
        /// </summary>
        public static int scan_close_code_fence(string s, int pos, int len)
        {
            /*!re2c
              ([`]{3,} | [~]{3,}) / spacechar* [\n]
                                          { if (p - start > len) {
                                            return (p - start);
                                          } else {
                                            return 0;
                                          } }
              .? { return 0; }
            */
            var p = MatchRegex(s, pos, close_code_fence);
            if (p > len)
                return p;

            return 0;
        }

        /// <summary>
        /// Scans an entity.
        /// Returns number of chars matched.
        /// </summary>
        public static int scan_entity(string s, int pos)
        {
            /*!re2c
              [&] ([#] ([Xx][A-Fa-f0-9]{1,8}|[0-9]{1,8}) |[A-Za-z][A-Za-z0-9]{1,31} ) [;]
                 { return (p - start); }
              .? { return 0; }
            */

            if (pos + 3 >= s.Length)
                return 0;

            if (s[pos] != '&')
                return 0;

            char c;
            int i;
            int counter = 0;
            if (s[pos + 1] == '#')
            {
                c = s[pos + 2];
                if (c == 'x' || c == 'X')
                {
                    // expect 1-8 hex digits starting from pos+3
                    for (i = pos + 3; i < s.Length; i++)
                    {
                        c = char.ToUpperInvariant(s[i]);
                        if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
                        {
                            if (++counter == 9)
                                return 0;

                            continue;
                        }

                        if (c == ';')
                            return counter == 0 ? 0 : i - pos + 1;

                        return 0;
                    }
                }
                else
                {
                    // expect 1-8 digits starting from pos+2
                    for (i = pos + 2; i < s.Length; i++)
                    {
                        c = s[i];
                        if (c >= '0' && c <= '9')
                        {
                            if (++counter == 9)
                                return 0;

                            continue;
                        }

                        if (c == ';')
                            return counter == 0 ? 0 : i - pos + 1;

                        return 0;
                    }
                }
            }
            else
            {
                // expect a letter and 1-31 letters or digits
                c = char.ToUpperInvariant(s[pos + 1]);
                if (c < 'A' || c > 'Z')
                    return 0;

                for (i = pos + 2; i < s.Length; i++)
                {
                    c = char.ToUpperInvariant(s[i]);
                    if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))
                    {
                        if (++counter == 32)
                            return 0;

                        continue;
                    }

                    if (c == ';')
                        return counter == 0 ? 0 : i - pos + 1;

                    return 0;
                }
            }

            return 0;
        }
    }
}
