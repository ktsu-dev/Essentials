## v2.10.5 (patch)

Changes since v2.10.4:

- Wait on the test's cancellation token instead of Thread.Sleep ([@Claude](https://github.com/Claude))
- Give the cancellation test room for a slow runner to deliver the token ([@Claude](https://github.com/Claude))
- Drop the outer cancellation catch that the inner one made unreachable ([@Claude](https://github.com/Claude))
- Kill the child when ExecuteAsync is cancelled, as Execute does [patch] ([@Claude](https://github.com/Claude))

