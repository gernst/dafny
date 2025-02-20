//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny {
  public class Advice {
    public enum Target {
      Bool,
      Char,
      Int,
      Real,
      String,
      Object
    }

    public readonly PreType PreType;
    public readonly Target What;

    public string WhatString => What == Target.Object ? PreType.TypeNameObjectQ : What.ToString().ToLower();

    public Advice(PreType preType, Target advice) {
      PreType = preType;
      What = advice;
    }

    public bool Apply(PreTypeResolver preTypeResolver) {
      if (PreType.Normalize() is PreTypeProxy proxy) {
        ActOnAdvice(proxy, preTypeResolver);
        return true;
      }
      return false;
    }

    public bool ApplyFor(PreTypeProxy proxy, PreTypeResolver preTypeResolver) {
      if (PreType.Normalize() == proxy) {
        ActOnAdvice(proxy, preTypeResolver);
        return true;
      }
      return false;
    }

    private void ActOnAdvice(PreTypeProxy proxy, PreTypeResolver preTypeResolver) {
      // Note, the following debug print may come out _before_ the "Type inference state ..." header, if ActOnAdvice is called
      // during what is only a partial constraint solving round.
      preTypeResolver.Constraints.DebugPrint($"    DEBUG: acting on advice, setting {proxy} := {WhatString}");

      Type StringDecl() {
        var s = preTypeResolver.resolver.moduleInfo.TopLevels["string"];
        return new UserDefinedType(s.tok, s.Name, s, new List<Type>());
      }

      var target = What switch {
        Target.Bool => preTypeResolver.Type2PreType(Type.Bool),
        Target.Char => preTypeResolver.Type2PreType(Type.Char),
        Target.Int => preTypeResolver.Type2PreType(Type.Int),
        Target.Real => preTypeResolver.Type2PreType(Type.Real),
        Target.String => preTypeResolver.Type2PreType(StringDecl()),
        Target.Object => preTypeResolver.Type2PreType(preTypeResolver.resolver.SystemModuleManager.ObjectQ()),
        _ => throw new cce.UnreachableException() // unexpected case
      };
      proxy.Set(target);
    }
  }
}
