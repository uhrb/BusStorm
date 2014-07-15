using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace BusStorm.Sockets
{
  public static class Extensions
  {
    public static async Task<T> SendAndReceiveAsync<T>(
      this BusClientSocket<T> connection,
      T messageToSend,
      Func<T, bool> sequenceSelector)
      where T : BusStormMessageBase
    {
      var eve = new ManualResetEvent(false);
      T receivedMessage = null;
      var sub = connection.ReceivedMessages.Where(sequenceSelector)
        .Subscribe(next =>
        {
          receivedMessage = next;
          eve.Set();
        });
      await connection.SendAsync(messageToSend);
      eve.WaitOne();
      sub.Dispose();
      return receivedMessage;
    }

    public static async Task<T> SendAndReceiveAsync<T>(
      this BusClientSocket<T> connection,
      T messageToSend,
      Func<T, T, bool> sequenceSelector)
      where T : BusStormMessageBase
    {
      var eve = new ManualResetEvent(false);
      T receivedMessage = null;
      var sendedMessage = messageToSend;
      var sub = connection.ReceivedMessages.Where(l => sequenceSelector(sendedMessage, l))
        .Subscribe(next =>
        {
          receivedMessage = next;
          eve.Set();
        });
      await connection.SendAsync(messageToSend);
      eve.WaitOne();
      sub.Dispose();
      return receivedMessage;
    }

    public static async Task<IEnumerable<T>> SendAndReceiveManyAsync<T>(
      this BusClientSocket<T> connection,
      T messageToSend,
      Func<T, T, bool> sequenceSelector, 
      Func<T, IEnumerable<T>, bool> sequenceCompletedSelector,
      bool continueOnError)
      where T : BusStormMessageBase
    {
      var eve = new ManualResetEvent(false);
      var lst = new BlockingCollection<T>();
      var sendedMessage = messageToSend;
      var sub = connection.ReceivedMessages.Where(l => sequenceSelector(sendedMessage, l))
        .Subscribe(
          next =>
          {
            if (lst.IsAddingCompleted)
            {
              return;
            }

            lock (lst)
            {
              lst.Add(next);
              if (!sequenceCompletedSelector(sendedMessage, lst))
              {
                return;
              }

              lst.CompleteAdding();
            }

            eve.Set();
          },
          error =>
          {
            if (continueOnError)
            {
              return;
            }

            lst.CompleteAdding();
            eve.Set();
          },
          () =>
          {
            lst.CompleteAdding();
            eve.Set();
          });
      await connection.SendAsync(messageToSend);
      eve.WaitOne();
      sub.Dispose();
      return lst;
    }

    public static async Task<IEnumerable<T>> SendAndReceiveManyAsync<T>(
      this BusClientSocket<T> connection,
      T messageToSend,
      Func<T, T, bool> sequenceSelector,
      Func<T, T, bool> sequenceCompletedSelector,
      bool continueOnError)
      where T : BusStormMessageBase
    {
      var eve = new ManualResetEvent(false);
      var lst = new BlockingCollection<T>();
      var sendedMessage = messageToSend;
      var sub = connection.ReceivedMessages.Where(
        l => sequenceSelector(sendedMessage, l))
        .Subscribe(
          next =>
          {
            if (lst.IsAddingCompleted)
            {
              return;
            }

            if (!sequenceCompletedSelector(sendedMessage, next))
            {
              lst.Add(next);
              return;
            }

            lock (lst)
            {
              lst.Add(next);
              lst.CompleteAdding();
            }

            eve.Set();
          },
          error =>
          {
            if (continueOnError)
            {
              return;
            }

            lst.CompleteAdding();
            eve.Set();
          },
          () =>
          {
            lst.CompleteAdding();
            eve.Set();
          });
      await connection.SendAsync(messageToSend);
      eve.WaitOne();
      sub.Dispose();
      return lst;
    }

    public static async Task<IObservable<T>> SendAndReceiveManyObserve<T>(
      this BusClientSocket<T> connection,
      T messageToSend,
      Func<T, T, bool> sequenceSelector,
      Func<T, T, bool>
        sequenceCompletedSelector)
      where T : BusStormMessageBase
    {
      var sendedMessage = messageToSend;
      ISubject<T, T> subj = null;
      subj = Subject.Create(
        Observer.Create<T>(
          a =>
          {
            if (sequenceCompletedSelector(messageToSend, a))
            {
              subj.OnCompleted();
            }
          },
          error => subj.OnError(error),
          () => subj.OnCompleted()),
        connection.ReceivedMessages.Where(l => sequenceSelector(sendedMessage, l)));
      await connection.SendAsync(messageToSend);
      return subj;
    }
  }
}