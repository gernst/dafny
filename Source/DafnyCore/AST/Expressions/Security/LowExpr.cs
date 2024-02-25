using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny; 

public class LowExpr : Expression, ICloneable<LowExpr>, ICanFormat {
  [Peer]
  public readonly Expression E;
  void ObjectInvariant() {
    Contract.Invariant(E != null);
  }
  
  [Captured]
  public LowExpr(IToken tok, Expression expr) : base(tok)
  {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    cce.Owner.AssignSame(this, expr);
    E = expr;
  }

  public LowExpr(Cloner cloner, LowExpr original) : base(cloner, original) {
    E = cloner.CloneExpr(original.E);
  }

  public LowExpr Clone(Cloner cloner) {
    return new LowExpr(cloner, this);
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
  
  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    return formatter.SetIndentParensExpression(indentBefore, OwnedTokens);
  }
}