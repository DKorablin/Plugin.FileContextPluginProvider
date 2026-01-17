# Code Style Mandates (Synchronized with .editorconfig)

Always adhere to these specific C# styles derived from our project configuration:

- **Indentation:** Use **Tabs**, not spaces.
- **Type Naming:** Do NOT use language keywords. Use BCL types exclusively (e.g., use `Int32` instead of `int`, `String` instead of `string`, `Boolean` instead of `bool`).
- **Member Access:** Always prefix global non static field, method, and property calls with `this.`.

- **Control Flow:**
  - Omit curly braces `{}` for all single-line `if`, `foreach`, `for`, and `while` statements.
  - Keep single-line statements on one line where possible.

- **Logic:** Favor ternary operators (`? :`) over standard `if-else` assignments or return statements.
- **Namespaces:** Place all `using` directives **outside** of the namespace declaration.
- **Variables:** Avoid the `var` keyword; explicitly state the type using BCL names.

- **Documentation**
- Do not expand summary blocks beyond one line.