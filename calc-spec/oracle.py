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
        if c in "+-*/^()%":
            tokens.append(("op", c, i))
            i += 1
            continue
        raise CalcError("parse-error", i)
    tokens.append(("end", "", len(expr)))
    return tokens


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
        kind, _, pos = self.peek()
        if kind != "end":
            raise CalcError("trailing", pos)
        return value

    def expr(self, depth):
        v = self.term(depth)
        while True:
            kind, val, _ = self.peek()
            if kind == "op" and val in "+-":
                self.next()
                r = self.term(depth)
                v = v + r if val == "+" else v - r
            else:
                return v

    def term(self, depth):
        v = self.factor(depth)
        while True:
            kind, val, _ = self.peek()
            if kind == "op" and val in "*/":
                self.next()
                r = self.factor(depth)
                if val == "*":
                    v = v * r
                else:
                    if r == 0:
                        raise CalcError("division-by-zero")
                    v = v / r
            else:
                return v

    def factor(self, depth):
        kind, val, _ = self.peek()
        if kind == "op" and val == "-":
            self.next()
            return -self.factor(depth)
        if kind == "op" and val == "+":
            self.next()
            return self.factor(depth)
        base = self.postfix(depth)
        kind, val, _ = self.peek()
        if kind == "op" and val == "^":
            self.next()
            exp = self.factor(depth)  # right associative
            if exp.denominator != 1 or abs(exp) > MAXEXP:
                raise CalcError("exponent")
            n = int(exp)
            if n >= 0:
                return base ** n
            if base == 0:
                raise CalcError("division-by-zero")
            return Fraction(1, 1) / (base ** (-n))
        return base

    def postfix(self, depth):
        v = self.atom(depth)
        while True:
            kind, val, _ = self.peek()
            if kind == "op" and val == "%":
                self.next()
                v = v / 100
            else:
                return v

    def atom(self, depth):
        if depth > MAXDEPTH:
            raise CalcError("too-deep")
        kind, val, pos = self.next()
        if kind == "num":
            return parse_number(val, pos)
        if kind == "ident":
            if val not in self.names:
                raise CalcError("unknown-identifier", pos)
            return self.names[val]
        if kind == "op" and val == "(":
            v = self.expr(depth + 1)
            k2, v2, p2 = self.next()
            if not (k2 == "op" and v2 == ")"):
                raise CalcError("parenthesis", p2)
            return v
        raise CalcError("parse-error", pos)


def evaluate(expr, names=None):
    """Evaluate one expression per spec; returns Fraction or raises CalcError."""
    parsed_names = {k: parse_number(str(v), 0) for k, v in (names or {}).items()}
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
        result = evaluate(inp, vec.get("names", {}))
        got = {"status": "ok", "rational": f"{result.numerator}/{result.denominator}"}
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
