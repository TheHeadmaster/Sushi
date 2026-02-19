Sushi
===================================

**Sushi** is a strongly-typed programming language that compiles directly into Assembly. It is Ahead-of-Time (AOT) and does not have a runtime.

.. note::

   This project is under active development.

Why Sushi?
-==========
Sushi is meant to solve problems that normally occur in other languages. Sushi does not solve every problem, of course -- no language can -- but it is
meant to solve major pain points that impact development. So what is it meant to solve? Sushi's core design concepts prioritize these beliefs.

Speed
^==========
Sushi needs to be fast. It doesn't need to be the *fastest* language, per se, but a core design concept is that a developer should be thinking about whether
their program is designed to be fast, not their language. Every single overhead and cpu cycle not explicitly signed-up for by the developer should have
to justify its existence.

Readability
^==========
Sushi should be human-readable and easy to understand. The least amount of time should be spent trying to hold a bunch of heiroglyphic syntax in your head
so that you can focus on the meaning of the code.

Power
^==========
It should be easy in Sushi to do the thing you're trying to do -- hard problems sometimes require complex code. However, if easy problems require complex code,
then the language is sabatoging you. Therefore, Sushi strives to give you the tools needed to focus on doing the thing you're trying to do, and not how you
can massage the code to get it working.

Predictability
^==========
Sushi should be able to behave as expected (when possible, no language is magic). Sushi should not have any features or syntax that introduce footguns in
code. Sushi should *also* introduce features and syntax that specifically prevent footguns that are normally commonplace. This should also be apparent
at compile time. If you put a footgun in your code, then Sushi should *not* let you compile it. So, it should be type-safe, memory-safe, and thread-safe
at compile time. No nulls, no red and blue functions, no splitting your code in half. The developer should be able to think at the highest level,
and know that their code will behave as expected at the lowest level. We can't remove every edge case, but the less edge cases the developer has to guess
the better.

Portability
^==========
Sushi executables should have the ability to run anywhere (as long as it is built for that architecture) without any setup from the user. That means
that they should not need to install a runtime or have it running in the background (unless you're using the interactive interpreter).

Developer Ease of Use
^==========
Sushi should be easy to set up a development environment for, build, run, and manage dependencies. It should never be required to fight with some
arcane toolchain in order to get a hello world. No dependency hell, no linking nightmares, no double-include bloat or any of the other nightmares.

Freedom
^==========
Now, speed is the hardest to make coexist with all of these other promises, as you inevitably give up some speed for abstraction and guard rails.
Sushi should let you, if you absolutely *have* to, go into the lower levels and do highly optimized code, but forces you to opt into the footguns.
You use it at your own risk, but only if you convince the language you absolutely know what you're signing up for.


Contents
--------

.. toctree::

   Home <self>
