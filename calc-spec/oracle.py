#!/usr/bin/env python3
"""Oracle de référence pour nomi-calc-spec/1.0.

Implémente la spécification en fractions exactes (stdlib `fractions`) et sert à :
1. générer/corriger les résultats attendus de vectors.json (--generate) ;
2. valider la cohérence interne du corpus (--validate).

Ce n'est PAS le noyau produit : c'est l'oracle indépendant du corpus.
Les implémentations C#/Swift ne doivent pas partager ce code.
"""
import json
import re
import sys
from fractions import Fraction
from pathlib import Path

MAXLEN = 200
MAXEXP = 64
MAXDEPTH = 32


class CalcError(Exception):
    def __init__(self, code, pos=None):
        self.code = code
        self.pos = pos
        super().__init__(code)


GROUPING = re.compile(r"(?<=\d) (?=\d{3}(?!\d))")


def normalize(expr):
    e = expr.replace("×", "*").replace("÷", "/").replace("−", "-")
    e = e.replace("\u202f", "").replace("\u00a0", "")
    # Reading boundary: a simple space between groups of exactly 3 digits is a
    # thousands separator and is removed before tokenisation.
    previous = None
    while previous != e:
        previous = e
        e = GROUPING.sub("", e)
    return e


def tokenize(expr):
    if len(expr) > MAXLEN:
        raise CalcError("too-long")
    tokens = []
    i = 0
    while i < len(expr):
        c = expr[i]
        if c.isspace():
            i += 1
            continue
        if c.isdigit() or c == ".":
            j = i
            while j < len(expr) and (expr[j].isdigit() or expr[j] in ".,"):
                j += 1
            tokens.append(("num", expr[i:j], i))
            i = j
            continue
        if c.isalpha() or c == "_":
            j = i
            while j < len(expr) and (expr[j].isalnum() or expr[j] == "_"):
                j += 1
            tokens.append(("ident", expr[i:j], i))
            i = j
            continue
        if c in "€$£":
            tokens.append(("cur", c, i))
            i += 1
            continue
        if c in "+-*/^()%":
            tokens.append(("op", c, i))
            i += 1
            continue
        raise CalcError("parse-error", i)
    tokens.append(("end", "", len(expr)))
    return merge_currency(tokens)


CURRENCY_CODES = {"€": "EUR", "$": "USD", "£": "GBP"}


def merge_currency(tokens):
    """Attach currency symbols to the adjacent number — SPEC 3.2: prefix or
    suffix, with or without space. A symbol between two numbers (`12€50`) is
    `unsupported`; a lone symbol is `parse-error`."""
    merged = []
    ahead = list(tokens)
    for i, tok in enumerate(ahead):
        kind, val, pos = tok[0], tok[1], tok[2]
        if kind != "cur":
            merged.append(tok)
            continue
        before = merged and merged[-1][0] == "numcur" or merged and merged[-1][0] == "num"
        after = i + 1 < len(ahead) and ahead[i + 1][0] == "num"
        if before and after:
            raise CalcError("unsupported", pos)
        if not before and not after:
            raise CalcError("parse-error", pos)
        code = CURRENCY_CODES[val]
        if before:
            raw, p = merged[-1][1], merged[-1][2]
            if merged[-1][0] == "numcur":
                raise CalcError("parse-error", pos)
            merged[-1] = ("numcur", raw, p, code)
        else:
            raw, p = ahead[i + 1][1], ahead[i + 1][2]
            if ahead[i + 1][0] == "numcur":
                raise CalcError("parse-error", pos)
            ahead[i + 1] = ("numcur", raw, p, code)
    return merged


def parse_number(tok, pos):
    # Canonical internal form: dot decimal separator, no grouping. At the kernel
    # boundary, commas have already been normalized per locale rules; a comma
    # here is treated as decimal comma (french reading boundary).
    t = tok.replace(",", ".")
    if t.count(".") > 1:
        raise CalcError("parse-error", pos)
    try:
        return Fraction(t)
    except (ValueError, ZeroDivisionError):
        raise CalcError("parse-error", pos)


def dim_add(a, b, add):
    """a, b: (Fraction, dim) — SPEC 3.1: identical dimensions required."""
    check_compatible(a[1], b[1])
    return (a[0] + b[0] if add else a[0] - b[0], a[1])


def dim_mul(a, b):
    if a[1] is None:
        dim = b[1]
    elif b[1] is None:
        dim = a[1]
    else:
        raise CalcError("dimension-mismatch")
    return (a[0] * b[0], dim)


def dim_div(a, b):
    if b[0] == 0:
        raise CalcError("division-by-zero")
    if a[1] is None and b[1] is not None:
        raise CalcError("dimension-mismatch")
    if a[1] is not None and b[1] is not None:
        check_compatible(a[1], b[1])
        return (a[0] / b[0], None)
    return (a[0] / b[0], a[1])


def check_compatible(da, db):
    ka = da[0] if da else None
    kb = db[0] if db else None
    if ka != kb:
        raise CalcError("dimension-mismatch")
    if ka == "currency" and da[1] != db[1]:
        raise CalcError("currency-mixing")
    if ka == "duration" and da[1] != db[1]:
        raise CalcError("dimension-mismatch")


class Parser:
    def __init__(self, tokens, names):
        self.tokens = tokens
        self.at = 0
        self.names = names

    def peek(self):
        return self.tokens[self.at]

    def next(self):
        t = self.tokens[self.at]
        self.at += 1
        return t

    def parse(self, depth=0):
        value = self.expr(depth)
        token = self.peek()
        kind, val, pos = token[0], token[1], token[2]
        if kind != "end":
            # SPEC 2.1 : une fermante sans ouvrante est `parenthesis` ;
            # `trailing` ne concerne que les autres jetons résiduels.
            if kind == "op" and val == ")":
                raise CalcError("parenthesis", pos)
            raise CalcError("trailing", pos)
        return value

    def expr(self, depth):
        v = self.term(depth)
        while True:
            token = self.peek()
            kind, val = token[0], token[1]
            if kind == "op" and val in "+-":
                self.next()
                r = self.term(depth)
                v = dim_add(v, r, val == "+")
            else:
                return v

    def term(self, depth):
        v = self.factor(depth)
        while True:
            token = self.peek()
            kind, val = token[0], token[1]
            if kind == "op" and val in "*/":
                self.next()
                r = self.factor(depth)
                if val == "*":
                    v = dim_mul(v, r)
                else:
                    v = dim_div(v, r)
            else:
                return v

    def factor(self, depth):
        token = self.peek()
        kind, val = token[0], token[1]
        if kind == "op" and val == "-":
            self.next()
            negated = self.factor(depth)
            return (-negated[0], negated[1])
        if kind == "op" and val == "+":
            self.next()
            return self.factor(depth)
        base = self.postfix(depth)
        token = self.peek()
        kind, val = token[0], token[1]
        if kind == "op" and val == "^":
            self.next()
            exp = self.factor(depth)  # right associative
            # SPEC 3.1 (v1.1) : exponentiation d'une valeur dimensionnée — contresens.
            if base[1] is not None or exp[1] is not None:
                raise CalcError("dimension-mismatch")
            if exp[0].denominator != 1 or abs(exp[0]) > MAXEXP:
                raise CalcError("exponent")
            n = int(exp[0])
            if n >= 0:
                return (base[0] ** n, None)
            if base[0] == 0:
                raise CalcError("division-by-zero")
            return (Fraction(1, 1) / (base[0] ** (-n)), None)
        return base

    def postfix(self, depth):
        v = self.atom(depth)
        while True:
            token = self.peek()
            kind, val = token[0], token[1]
            if kind == "op" and val == "%":
                self.next()
                v = (v[0] / 100, v[1])  # SPEC v1.1 : % conserve la dimension
            else:
                return v

    def atom(self, depth):
        if depth > MAXDEPTH:
            raise CalcError("too-deep")
        token = self.next()
        kind, val, pos = token[0], token[1], token[2]
        if kind == "num":
            return (parse_number(val, pos), None)
        if kind == "numcur":
            return (parse_number(val, pos), ("currency", token[3]))
        if kind == "ident":
            if val not in self.names:
                raise CalcError("unknown-identifier", pos)
            return self.names[val]
        if kind == "op" and val == "(":
            v = self.expr(depth + 1)
            closing = self.next()
            if not (closing[0] == "op" and closing[1] == ")"):
                raise CalcError("parenthesis", closing[2])
            return v
        raise CalcError("parse-error", pos)


def evaluate(expr, names=None):
    """Evaluate one expression per spec; returns (Fraction, dim) or raises CalcError."""
    parsed_names = {k: (parse_number(str(v), 0), None) for k, v in (names or {}).items()}
    tokens = tokenize(normalize(expr))
    return Parser(tokens, parsed_names).parse()


def round_half_up(fr, places):
    """Round half away from zero at given decimal places; exact."""
    scaled = fr * (10 ** places)
    floor = scaled.numerator // scaled.denominator
    rem = scaled - floor
    if rem >= Fraction(1, 2):
        floor += 1
    return Fraction(floor, 10 ** places)


def group_thousands(intpart):
    """French display rule (SPEC 1.4): group integer part by three with U+202F
    narrow no-break spaces whenever it has more than 3 digits."""
    if len(intpart) <= 3:
        return intpart
    groups = []
    while intpart:
        groups.append(intpart[-3:])
        intpart = intpart[:-3]
    return "\u202f".join(reversed(groups))


def format_fr(fr, places=4):
    rounded = round_half_up(fr, places)
    sign = "-" if rounded < 0 else ""
    rounded = abs(rounded)
    num, den = rounded.numerator, rounded.denominator
    # exact decimal expansion is possible only if den = 2^a*5^b; otherwise we
    # take exactly `places` digits from the rounded value (which by construction
    # has at most `places` decimals... not guaranteed after reduction, so expand).
    scale = 10 ** places
    scaled_num = num * scale
    q, r = divmod(scaled_num, den)
    digits = str(q).rjust(places + 1, "0")
    intpart, fracpart = digits[:-places] or "0", digits[-places:]
    intpart = group_thousands(intpart)
    fracpart = fracpart.rstrip("0")
    if not fracpart:
        return sign + intpart
    return sign + intpart + "," + fracpart


def run_vector(vec):
    inp = vec["input"]
    expect = vec["expect"]
    try:
        result, dim = evaluate(inp, vec.get("names", {}))
        got = {"status": "ok", "rational": f"{result.numerator}/{result.denominator}"}
        if dim is None:
            got["dimension"] = "none"
        elif dim[0] == "currency":
            got["dimension"] = {"currency": dim[1]}
        else:
            got["dimension"] = {"duration": dim[1]}
        if "display_fr" in expect:
            places = expect.get("display_places", 4)
            got["display_fr"] = format_fr(result, places)
    except CalcError as e:
        got = {"status": "error", "error": e.code}
    return got


def check_vector(vec):
    expect = vec["expect"]
    got = run_vector(vec)
    problems = []
    if expect["status"] == "ok":
        if got["status"] != "ok":
            problems.append(f"expected ok, got error {got.get('error')}")
        else:
            if got["rational"] != expect["rational"]:
                problems.append(f"rational {got['rational']} != {expect['rational']}")
            if "display_fr" in expect and got.get("display_fr") != expect["display_fr"]:
                problems.append(f"display {got.get('display_fr')!r} != {expect['display_fr']!r}")
            if "dimension" in expect and got.get("dimension") != expect["dimension"]:
                problems.append(f"dimension {got.get('dimension')!r} != {expect['dimension']!r}")
    else:
        if got["status"] != "error":
            problems.append(f"expected error {expect.get('error')}, got ok {got.get('rational')}")
        elif expect.get("error") and got["error"] != expect["error"]:
            problems.append(f"error {got['error']} != {expect['error']}")
    return problems


def main():
    path = Path(__file__).parent / "vectors.json"
    corpus = json.loads(path.read_text(encoding="utf-8"))
    total, failed = 0, 0
    for vec in corpus["vectors"]:
        total += 1
        problems = check_vector(vec)
        if problems:
            failed += 1
            print(f"FAIL {vec['id']}: {'; '.join(problems)}")
    print(f"{total - failed}/{total} vectors internally consistent")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
