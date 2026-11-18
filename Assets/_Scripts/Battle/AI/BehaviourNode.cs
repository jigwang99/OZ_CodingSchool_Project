using System;
using System.Collections.Generic;

namespace AutoBattler.Battle.AI
{
    public enum BehaviourStatus { Success, Failure, Running }

    public abstract class BehaviourNode
    {
        public BehaviourStatus LastStatus { get; private set; }
        public BehaviourStatus Tick() => LastStatus=Evaluate();
        protected abstract BehaviourStatus Evaluate();
    }
    public sealed class ConditionNode : BehaviourNode
    {
        private readonly Func<bool> condition;
        public ConditionNode(Func<bool> condition)=>this.condition=condition;
        protected override BehaviourStatus Evaluate()=>condition()?BehaviourStatus.Success:BehaviourStatus.Failure;
    }
    public sealed class ActionNode : BehaviourNode
    {
        private readonly Func<BehaviourStatus> action;
        public ActionNode(Func<BehaviourStatus> action)=>this.action=action;
        protected override BehaviourStatus Evaluate()=>action();
    }
    /// <summary>매 Tick 첫 자식부터 평가. 실패나 실행 중인 자식에서 중단.</summary>
    public sealed class SequenceNode : BehaviourNode
    {
        private readonly BehaviourNode[] children;
        public IReadOnlyList<BehaviourNode> Children { get; }
        public SequenceNode(params BehaviourNode[] children)
        { this.children=(BehaviourNode[])children.Clone(); Children=Array.AsReadOnly(this.children); }
        protected override BehaviourStatus Evaluate()
        {
            foreach(BehaviourNode child in children)
            {
                BehaviourStatus result=child.Tick();
                if(result!=BehaviourStatus.Success) return result;
            }
            return BehaviourStatus.Success;
        }
    }
    /// <summary>매 Tick 우선순위가 높은 자식부터 평가. 성공이나 실행 중이면 선택 완료.</summary>
    public sealed class SelectorNode : BehaviourNode
    {
        private readonly BehaviourNode[] children;
        public IReadOnlyList<BehaviourNode> Children { get; }
        public SelectorNode(params BehaviourNode[] children)
        { this.children=(BehaviourNode[])children.Clone(); Children=Array.AsReadOnly(this.children); }
        protected override BehaviourStatus Evaluate()
        {
            foreach(BehaviourNode child in children)
            {
                BehaviourStatus result=child.Tick();
                if(result!=BehaviourStatus.Failure) return result;
            }
            return BehaviourStatus.Failure;
        }
    }
}
