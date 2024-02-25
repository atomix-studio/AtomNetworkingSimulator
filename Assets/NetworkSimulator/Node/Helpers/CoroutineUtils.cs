using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ITF.Utils
{
    public static class CoroutineUtils
    {        
        public static IEnumerator RunIterator(IEnumerator enumerator, Action done)
        {
            while (true)
            {
                if (enumerator.MoveNext() == false)
                {
                    break;
                }
                var current = enumerator.Current;
                yield return current;
            }

            done();
        }

        /// <summary>
        /// Run an iterator function that might throw an exception. Call the callback with the exception
        /// if it does or null if it finishes without throwing an exception.
        /// </summary>
        /// <param name="enumerator">Iterator function to run</param>
        /// <param name="done">Callback to call when the iterator has thrown an exception or finished.
        /// The thrown exception or null is passed as the parameter.</param>
        /// <returns>An enumerator that runs the given enumerator</returns>
        public static IEnumerator RunThrowingIterator(IEnumerator enumerator, Action<Exception> done)
        {
            while (true)
            {
                object current;
                try
                {
                    if (enumerator.MoveNext() == false)
                    {
                        break;
                    }
                    current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.Message);
                    done(ex);
                    yield break;
                }
                yield return current;
            }
            done(null);
        }

        /// <summary>
        /// Run la coroutine via une TaskCompletionSource, attention aux deadlocks, voir pour le TaskCreationOptions.RunContinuationsAsynchronously
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static async Task<bool> RunAsyncIterator(this MonoBehaviour monoBehaviour, IEnumerator enumerator)
        {
            var tcs = new TaskCompletionSource<bool>();
            monoBehaviour.StartCoroutine(RunIterator(enumerator, () =>
            {
                tcs.TrySetResult(true);
            }));

            return await tcs.Task;
        }

        /// <summary>
        /// Run la coroutine via une TaskCompletionSource, attention aux deadlocks, voir pour le TaskCreationOptions.RunContinuationsAsynchronously
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static async Task<bool> RunAsyncIterator(Func<IEnumerator, Coroutine> coroutineStarter, IEnumerator enumerator)
        {
            var tcs = new TaskCompletionSource<bool>();
            coroutineStarter(RunIterator(enumerator, () =>
            {
                tcs.TrySetResult(true);
            }));

            return await tcs.Task;
        }

        /// <summary>
        /// Run la coroutine dans un try-catch via une TaskCompletionSource, attention aux deadlocks, voir pour le TaskCreationOptions.RunContinuationsAsynchronously
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static async Task<bool> RunAsyncThrowingIterator(this MonoBehaviour monoBehaviour, IEnumerator enumerator)
        {
            var tcs = new TaskCompletionSource<bool>();
            monoBehaviour.StartCoroutine(RunThrowingIterator(enumerator, (result) =>
            {
                tcs.TrySetResult(result == null);
            }));

            return await tcs.Task;
        }

        /// <summary>
        /// Run la coroutine dans un try-catch via une TaskCompletionSource, attention aux deadlocks, voir pour le TaskCreationOptions.RunContinuationsAsynchronously
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static async Task<bool> RunAsyncThrowingIterator(Func<IEnumerator, Coroutine> coroutineStarter, IEnumerator enumerator)
        {            
            var tcs = new TaskCompletionSource<bool>();
            coroutineStarter(RunThrowingIterator(enumerator, (result) =>
            {
                tcs.TrySetResult(result == null);
            }));

            return await tcs.Task;
        }

        /// <summary>
        /// Start a coroutine that might throw an exception. Call the callback with the exception if it
        /// does or null if it finishes without throwing an exception.
        /// </summary>
        /// <param name="monoBehaviour">MonoBehaviour to start the coroutine on</param>
        /// <param name="enumerator">Iterator function to run as the coroutine</param>
        /// <param name="done">Callback to call when the coroutine has thrown an exception or finished.
        /// The thrown exception or null is passed as the parameter.</param>
        /// <returns>The started coroutine</returns>
        public static Coroutine StartThrowingCoroutine(this MonoBehaviour monoBehaviour, IEnumerator enumerator, Action<Exception> done)
        {
            return monoBehaviour.StartCoroutine(RunThrowingIterator(enumerator, done));
        }

    }

}
