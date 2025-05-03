# Solver Tester

This is a small console application used to examine the manual rules for different rule seeds.

It can also be used to generate AIML files that can be used to provide the bot with the manual rules for any rule seed in case the C# library is unavailable.

## Arguments

`-g [rule seed] [path]` or `--generate [rule seed] [path]`: generates AIML files for the specified rule seed and outputs them to the specified directory. If omitted, outputs to `aiml[rule seed]` in the working directory.

## Commands

In interactive mode, the following commands are available:

`[module] rules [rule seed]`: displays part of the manual for the specified module.
`generate [rule seed] [path]`: same as the `-g` command line switch.

The following commands are for debugging purposes:

`[module] [rule seed] [text]`: calls the solver for the specified module to process the specified text.
`reset`: resets the AIML environment.
`set [predicate] [value]`: sets a user predicate.
`q`: ends the program.
