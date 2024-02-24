using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny; 

public class LowEventExpr : Expression, ICloneable<LowEventExpr>, ICanFormat {
  
  [Captured]
  public LowEventExpr(IToken tok) : base(tok) {
    Contract.Requires(tok != null);
  }

  public LowEventExpr(Cloner cloner, LowEventExpr original) : base(cloner, original) {
  }

  public LowEventExpr Clone(Cloner cloner) {
    return new LowEventExpr(cloner, this);
  }

  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    return formatter.SetIndentParensExpression(indentBefore, OwnedTokens);
  }
}