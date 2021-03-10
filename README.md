# DetectReferences

This tool takes 2 arguments:
- search directory
- output file

It attempts to load all assemblies (recursively) in the search directory, and checks if they reference a hardcoded list of assemblies (see program.cs).  If so, those references are written out to the output file.

# CollectReferences

This tool takes 2 arguments:
- an input file
- an output file

This takes in the output file from DetectReferences, and for each reference, it will determine which APIs (types/methods) are used by the referencing assembly.

## Special Note

CollectReferences depends on the RefDump tool, which is checked in to this repo via a submodule.  You must run `git submodule init` and `git submodule update` before building the solution.
