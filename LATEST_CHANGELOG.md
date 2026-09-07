## v2.4.0 (minor)

Changes since v2.3.0:

- refactor: move the disposed-stream setup out of the test body [patch] ([@Claude](https://github.com/Claude))
- fix: dispose the test's stream deterministically [patch] ([@Claude](https://github.com/Claude))
- test: cover the encoding providers' async failure paths [patch] ([@Claude](https://github.com/Claude))
- feat: make the encoding providers' stream paths genuinely async [minor] ([@Claude](https://github.com/Claude))
- test: build the missing directory path with Path.Join [patch] ([@Claude](https://github.com/Claude))
- test: cover the synchronous command paths SonarCloud found untested [patch] ([@Claude](https://github.com/Claude))
- fix: suppress KTSU0001 rather than reference System.Memory [patch] ([@Claude](https://github.com/Claude))
- fix: unbreak the build under ktsu.Sdk 2.28.0's analyzers [patch] ([@Claude](https://github.com/Claude))
- fix: make ICommandExecutor's synchronous path a real primitive [patch] ([@Claude](https://github.com/Claude))
- ci: make the SonarQube quality gate opt in [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- ci: adopt the unified dotnet workflow [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

