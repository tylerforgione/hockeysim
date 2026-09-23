# Code Conventions

## Naming

See the [config file](../.editorconfig) for naming practices.

## Types 

- Prefer explicit domain types when primitives would obscure meaning.
- Use `var` when the type is obvious from the right-hand side.
- Nullable reference types are enabled.
- Prefer immutable types where appropriate.

## Methods

- Methods should have one clear responsibility
    - If methods do too many things at once, think of using a helper
- Prefer guards over nested conditionals
- Keep public APIs small
- Method names should be informative, describing what the method does
    - If the name is too long, think of using a comment or about whether your function does too much

## Comments

Comments are not a necessity. They should only be used if a method is complicated or long. Comments should rarely be longer than one line. Emall explanations of how the method works should be avoided unless it needs to be clarified (e.g: multiple methods that do a similar thing two different ways).

Easily readable methods do not need comments; they should be understood by their name and the code within them. Variables should also not require comments.

Within a method, comments can explain what a piece of code does. Not every line needs a comment, and the comment should not be longer than the code. 

Do not leave any code commented out. That is what GitHub is for.

Use TODOs when there is a clearly defined plan for what needs to be implemented next.

## XML Documentation

Use XML documentation for:
- Public APIs where intent is not obvious
- Complex domain behavior
- Non-obvious parameters/return values

## Exceptions

- Do not catch exceptions only to ignore them
- Use domain-specific exceptions when appropriate
- Validate arguments at system boundaries

## Tests

- Test observable behaviour, not implementation details
- Test names should clearly describe what is being tested
- All new features should include tests for that new feature
- Bug fixes should include a regression test