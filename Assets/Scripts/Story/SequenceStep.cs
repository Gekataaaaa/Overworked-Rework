using System;
using System.Collections;

namespace Overworked
{
    /// <summary>
    /// Один шаг сценарной сцены (<see cref="ScriptedSequence"/>): реплика, сообщение, блок управления,
    /// галлюцинация и т. п. Шаги лежат в списке сценария через [SerializeReference] и добавляются
    /// в инспекторе кнопкой «Add step». Новый тип шага — наследник этого класса в Scripts/Story/Steps/.
    /// </summary>
    [Serializable]
    public abstract class SequenceStep
    {
        /// <summary>Короткая подпись шага в инспекторе: «Dialogue: F3_Start».</summary>
        public virtual string Describe() => GetType().Name.Replace("Step", "");

        /// <summary>Выполняет шаг. Сценарий ждёт, пока корутина закончится.</summary>
        public abstract IEnumerator Run(ScriptedSequence sequence);

        /// <summary>Имя объекта для подписи или «—», если поле пустое.</summary>
        protected static string NameOf(UnityEngine.Object o) => o != null ? o.name : "—";
    }
}
