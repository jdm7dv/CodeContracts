// CodeContracts
// 
// Copyright (c) Microsoft Corporation
// 
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;

namespace Microsoft.Research.CodeAnalysis
{
  using Provenance = IEnumerable<ProofObligation>;

  [ContractClass(typeof(ProofObligationContracts))]
  [ContractVerification(true)]
  public abstract class ProofObligation
  {
    #region Statics

    static private uint nextID = 0;

    #endregion

    #region Statistics

    public static int ProofObligationsWithCodeFix { get; private set; }
    
    #endregion

    #region State & invariant

    [ContractInvariantMethod]
    private void ObjectInvariant()
    {
      Contract.Invariant(this.codeFixes != null);
    }

    public readonly uint ID;
    protected readonly List<ICodeFix> codeFixes;
    private bool hasCodeFix;
    private bool hasSufficientAndNecessaryCondition;
    public readonly Provenance Provenance; // can be null
    public readonly string definingMethod; // can be null

    #endregion

    #region Constructor

    public ProofObligation(APC pc, string definingMethod, Provenance provenance)
    {
      this.PC = pc;
      this.definingMethod = definingMethod; // can be null 
      this.codeFixes = new List<ICodeFix>();
      this.hasCodeFix = false;
      this.hasSufficientAndNecessaryCondition = false;
      this.ID = nextID++;
      this.Provenance = provenance;
    }

    #endregion

    #region Public surface

    public readonly APC PC;

    protected IEnumerable<IInferredCondition> SufficientPreconditions { private set; get; }

    public void NotifySufficientYetNotNecessaryPreconditions(IEnumerable<IInferredCondition> sufficientPreconditions)
    {
      if(sufficientPreconditions != null && sufficientPreconditions.Any())
      {
        this.SufficientPreconditions = sufficientPreconditions;
      }
    }

    public void AddCodeFix(ICodeFix codeFix)
    {
      Contract.Requires(codeFix != null);

      if (!this.hasCodeFix)
      {
        this.hasCodeFix = true;
        ProofObligationsWithCodeFix++;
      }

      this.codeFixes.Add(codeFix);
    }

    public IEnumerable<ICodeFix> CodeFixes
    {
      get
      {
        Contract.Ensures(Contract.Result<IEnumerable<ICodeFix>>() != null);

        return this.codeFixes;
      }
    }

    public int CodeFixCount { get { return this.codeFixes.Count; } }
    public bool HasCodeFix { get { return this.CodeFixCount != 0; } }
    public bool HasASufficientAndNecessaryCondition
    {
      get
      {
        return this.hasSufficientAndNecessaryCondition;
      }
      set
      {
        Contract.Assume(!this.hasSufficientAndNecessaryCondition || value == true);
        this.hasSufficientAndNecessaryCondition = value;
      }
    }

    public bool InferredConditionContainsOnlyEnumValues { get; set; } // default: false

    public bool IsFromThrowInstruction { get; set; } // default: false

    public MinimalProofObligation MinimalProofObligation 
    { 
      get 
      { 
        return new MinimalProofObligation(this.PC, this.definingMethod, this.ConditionForPreconditionInference, this.ObligationName, this.Provenance, this.IsFromThrowInstruction); 
      } 
    }

    #endregion

    #region Client contracts

    /// <summary>
    /// Returns an expression standing for the condition encoded by this proof obligation.
    /// Can be null if the condition is not expressible as a BoxedExpression
    /// </summary>
    public abstract BoxedExpression Condition { get; }

    /// <summary>
    /// Sometimes we may need to know which kind of proof obligation it is 
    /// </summary>
    public abstract string ObligationName { get; }

    /// <summary>
    /// Returns an expression that shall be used for inferring a precondition or a code fix.
    /// In general it is the same as this.Condition, but some proof obligations can be smarter, and ask a different condition 
    /// Can be null if the condition is not expressible as a BoxedExpression.
    /// </summary>
    public virtual BoxedExpression ConditionForPreconditionInference { get { return this.Condition; } }

    public virtual APC PCForValidation { get { return this.PC; } }

    public virtual bool IsEmpty { get { return false;} }
 
    #endregion

  }

  #region Contracts for ProofObligation

  [ContractClassFor(typeof(ProofObligation))]
  abstract class ProofObligationContracts : ProofObligation
  {
    // dummy
    public ProofObligationContracts()
      : base(default(APC), null, null)
    {
    }

    public override BoxedExpression Condition
    {
      get { throw new NotImplementedException(); }
    }

    public override string ObligationName
    {
      get 
      {
        Contract.Ensures(Contract.Result<string>() != null);

        return null;
      }
    }
  }

  #endregion

  public class EmptyProofObligation : ProofObligation
  {
    public EmptyProofObligation(APC pc)
      : base(pc, null, null)
    {
    }

    public override bool IsEmpty
    {
      get
      {
        return true ;
      }
    }

    public override BoxedExpression Condition
    {
      get { return null; }
    }

    public override string ObligationName
    {
      get { return "Empty"; }
    }
  }

  public class MinimalProofObligation : ProofObligation
  {
    public const string InferredForwardObjectInvariant = "inferred forward object invariant";

    private readonly BoxedExpression condition;
    private readonly string obligationName;

    [ContractVerification(false)]
    public MinimalProofObligation(APC pc, string definingMethod, BoxedExpression condition, string obligationName, Provenance provenance, bool isFromThrowInstruction)
      : base(pc, definingMethod, provenance)
    {
      Contract.Requires(obligationName != null);
//      Contract.Requires(definingMethod != null);

      this.condition = condition;
      this.obligationName = obligationName;
      this.IsFromThrowInstruction = isFromThrowInstruction;
    }

    public static MinimalProofObligation GetFreshObligationForBoxingForwardObjectInvariant<Local, Parameter, Method, Field, Property, Event, Type, Attribute, Assembly>(APC entry, string methodName, IDecodeMetaData<Local, Parameter, Method, Field, Property, Event, Type, Attribute, Assembly> mdDecoder)
    {
      Contract.Requires(methodName != null);
      Contract.Requires(mdDecoder != null);
      Contract.Ensures(Contract.Result<MinimalProofObligation>() != null);

      return new MinimalProofObligation(entry, methodName, BoxedExpression.ConstBool(true, mdDecoder), MinimalProofObligation.InferredForwardObjectInvariant, null, false);
    }

    public override BoxedExpression Condition
    {
      get { return this.condition; }
    }

    public override string ObligationName
    {
      get { return this.obligationName; }
    }
  }

  public class FakeProofObligationForAssertionFromTheCache : ProofObligation
  {
    private readonly BoxedExpression condition;
    public object Method { get; private set; }

    public FakeProofObligationForAssertionFromTheCache(BoxedExpression condition, string definingMethod, object method)
      : base(APC.Dummy, definingMethod, null)
    {
      Contract.Requires(condition != null);
      // method can be null

      this.condition = condition;
      this.Method = method;
    }

    public override BoxedExpression Condition
    {
      get { return this.condition; }
    }

    public override string ObligationName
    {
      get { return "<fake proof obligation for inferred contract read from the cache>"; }
    }
  }

}
