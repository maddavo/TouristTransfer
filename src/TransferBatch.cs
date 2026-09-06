using System;
using System.Collections.Generic;

namespace TouristTransfer
{
    // This boundary keeps the transaction testable without loading Unity or a save.
    public interface ITransferContext<T>
    {
        bool IsEligible(T crew);
        bool HasSpace { get; }
        bool AtSource(T crew);
        bool AtDestination(T crew);
        void RemoveSource(T crew);
        bool AddDestination(T crew);
        void RemoveDestination(T crew);
        bool RestoreSource(T crew);
        void Notify(T crew);
    }

    public sealed class TransferResult
    {
        public int Moved;
        public int Remaining;
        public string Error;
    }

    public static class TransferBatch
    {
        public static TransferResult Run<T>(IEnumerable<T> candidates, ITransferContext<T> context)
        {
            var pending = new List<T>();
            var seen = new HashSet<T>();
            foreach (T crew in candidates)
                if (seen.Add(crew)) pending.Add(crew);
            var result = new TransferResult { Remaining = pending.Count };
            foreach (T crew in pending)
            {
                try
                {
                    if (!context.IsEligible(crew) || !context.AtSource(crew)) continue;
                    if (!context.HasSpace) break;
                    try
                    {
                        context.RemoveSource(crew);
                        if (!context.AddDestination(crew) || !context.AtDestination(crew))
                            throw new InvalidOperationException("Destination rejected the tourist.");
                    }
                    catch (Exception moveError)
                    {
                        try
                        {
                            if (context.AtDestination(crew)) context.RemoveDestination(crew);
                            if (!context.AtSource(crew) && !context.RestoreSource(crew))
                                throw new InvalidOperationException("Source rejected restoration.");
                            if (!context.AtSource(crew))
                                throw new InvalidOperationException("Tourist missing from source after restoration.");
                        }
                        catch (Exception restoreError)
                        {
                            throw new InvalidOperationException("Transfer and restoration failed. Stop and reload your pre-transfer save. " + restoreError.Message, moveError);
                        }
                        throw new InvalidOperationException("Transfer stopped; tourist restored to source. " + moveError.Message, moveError);
                    }
                    result.Moved++;
                    result.Remaining--;
                    // A listener failure must not roll back a committed transfer or repeat it.
                    context.Notify(crew);
                }
                catch (Exception error)
                {
                    result.Error = error.Message;
                    break;
                }
            }
            return result;
        }
    }
}
