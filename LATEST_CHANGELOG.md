## v2.10.2 (patch)

Changes since v2.10.1:

- Move the IConvertible fallback into the key-parsing helper too ([@Claude](https://github.com/Claude))
- Move non-IConvertible key parsing into its own helper ([@Claude](https://github.com/Claude))
- Detect zero in NextDown from its bits instead of a float comparison ([@Claude](https://github.com/Claude))
- List enum, TimeSpan and other non-IConvertible persistence keys [patch] ([@Claude](https://github.com/Claude))
- Keep NextDouble(min, max) below max and finite for wide bounds [patch] ([@Claude](https://github.com/Claude))
- Keep NextDoubleExclusive strictly below 1 at the top draw [patch] ([@Claude](https://github.com/Claude))

