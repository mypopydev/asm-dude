﻿// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AsmSim
{
    public class BranchInfoStore
    {
        #region Fields
        private readonly Tools _tools;
        private IDictionary<string, BranchInfo> _branchInfo;
        #endregion

        #region Constructors
        public BranchInfoStore(Tools tools)
        {
            this._tools = tools;
        }
        public BranchInfoStore(BranchInfoStore other)
        {
            this._tools = other._tools;
            if (other._branchInfo != null)
            {
                this._branchInfo = new Dictionary<string, BranchInfo>(other._branchInfo);
            }
        }
        #endregion

        public int Count { get { return (this._branchInfo == null) ? 0 : this._branchInfo.Count; } }

        public IEnumerable<BranchInfo> Values
        {
            get
            {
                if (this._branchInfo == null)
                {
                    yield break;
                }
                else
                {
                    foreach (var e in this._branchInfo.Values) yield return e;
                }
            }
        }

        public void Clear()
        {
            this._branchInfo?.Clear();
        }

        public static (BranchInfo BranchPoint1, BranchInfo BranchPoint2, BranchInfoStore MergedBranchInfo) RetrieveSharedBranchInfo(
            BranchInfoStore store1, BranchInfoStore store2, Tools tools)
        {
            //return RetrieveSharedBranchInfo_new(store1, store2, tools);
            return RetrieveSharedBranchInfo_OLD(store1, store2, tools);
        }

        public static (BranchInfo BranchPoint1, BranchInfo BranchPoint2, BranchInfoStore MergedBranchInfo) RetrieveSharedBranchInfo_new(
            BranchInfoStore store1, BranchInfoStore store2, Tools tools)
        {
            if (store1 == null) return (BranchPoint1: null, BranchPoint2: null, MergedBranchInfo: store2);
            if (store2 == null) return (BranchPoint1: null, BranchPoint2: null, MergedBranchInfo: store1);

            // foreach linenumber that is both in store1 and store2, check if they are dual, if so return both branch points

            ISet<int> lineNumbers1 = new HashSet<int>();
            foreach (var v in store1.Values) lineNumbers1.Add(v.LineNumber);

            foreach (var v in store2.Values)
            {
                if (lineNumbers1.Contains(v.LineNumber))
                {   // found a lineNumber that is both in store1 and store2
                    var t = RetrieveSharedBranchInfo(store1, store2, v.LineNumber, tools);
                    if (t.MergedBranchInfo != null)
                    {
                        return t; // we found a solution, return it;
                    }
                }
            }

            BranchInfoStore store = new BranchInfoStore(tools);
            store.Add(store1._branchInfo);
            store.Add(store2._branchInfo);
            return (null, null, store);
        }

        private static (BranchInfo BranchPoint1, BranchInfo BranchPoint2, BranchInfoStore MergedBranchInfo) RetrieveSharedBranchInfo(
            BranchInfoStore store1, BranchInfoStore store2, int lineNumber, Tools tools)
        {
            IList<BranchInfo> branchInfoList1 = new List<BranchInfo>(2);
            IList<BranchInfo> branchInfoList2 = new List<BranchInfo>(2);

            foreach (var b in store1.Values) if (b.LineNumber == lineNumber) branchInfoList1.Add(b);
            foreach (var b in store2.Values) if (b.LineNumber == lineNumber) branchInfoList2.Add(b);

            if (branchInfoList1.Count == 0) return (null, null, null);
            if (branchInfoList2.Count == 0) return (null, null, null);

            if ((branchInfoList1.Count == 1) && (branchInfoList2.Count == 1))
            {
                BranchInfo br1 = branchInfoList1[0];
                BranchInfo br2 = branchInfoList2[0];

                //Console.WriteLine("INFO: RetrieveSharedBranchInfo: br1=" + br1 + "; br2=" + br2);

                if (br1.BranchTaken && !br2.BranchTaken)
                {
                    return (BranchPoint1: br1, BranchPoint2: br2, MergedBranchInfo: new BranchInfoStore(tools));
                }
                else if (!br1.BranchTaken && br2.BranchTaken)
                {
                    return (BranchPoint1: br1, BranchPoint2: br2, MergedBranchInfo: new BranchInfoStore(tools));
                }
                else
                {
                    Console.WriteLine("WARNING: RetrieveSharedBranchInfo: A");
                }
            }
            Console.WriteLine("WARNING: RetrieveSharedBranchInfo: B");
            return (null, null, null);
        }

        public static (BranchInfo BranchPoint1, BranchInfo BranchPoint2, BranchInfoStore MergedBranchInfo) RetrieveSharedBranchInfo_OLD(BranchInfoStore store1, BranchInfoStore store2, Tools tools)
        {
            if (store1 == null) return (BranchPoint1: null, BranchPoint2: null, MergedBranchInfo: store2);
            if (store2 == null) return (BranchPoint1: null, BranchPoint2: null, MergedBranchInfo: store1);

            IList<string> sharedKeys = new List<string>();

            if ((store1._branchInfo != null) && (store2._branchInfo != null))
            {
                ICollection<string> keys1 = store1._branchInfo.Keys;
                ICollection<string> keys2 = store2._branchInfo.Keys;

                foreach (string key in keys1)
                {
                    if (keys2.Contains(key))
                    {
                        if (store1._branchInfo[key].BranchTaken != store2._branchInfo[key].BranchTaken)
                        {
                            sharedKeys.Add(key);
                        }
                        else
                        {
                            //Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: key " + key + " is shared but has equal branchTaken " + state1.BranchInfo[key].branchTaken);
                        }
                    }
                }
                Debug.Assert(sharedKeys.Count <= 1);

                if (false)
                {
                    foreach (string key in keys1)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state1 " + key);
                    }
                    foreach (string key in keys2)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state2 " + key);
                    }
                    foreach (string key in sharedKeys)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: sharedKey " + key);
                    }
                }
            }

            BranchInfo branchPoints1 = null;
            BranchInfo branchPoints2 = null;

            BranchInfoStore mergeBranchStore = new BranchInfoStore(tools);
            if (sharedKeys.Count == 0)
            {
                if (store1._branchInfo != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store1._branchInfo)
                    {
                        mergeBranchStore.Add(element.Value);
                    }
                }
                if (store2._branchInfo != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store2._branchInfo)
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value);
                        }
                    }
                }
                if (!tools.Quiet) Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: the two provided states do not share a branching point. This would happen in loops.");
            }
            else
            {
                //only use the first sharedKey

                string key = sharedKeys[0];
                branchPoints1 = store1._branchInfo[key];
                branchPoints2 = store2._branchInfo[key];

                foreach (KeyValuePair<string, BranchInfo> element in store1._branchInfo)
                {
                    if (!element.Key.Equals(key))
                    {
                        mergeBranchStore.Add(element.Value);
                    }
                }
                foreach (KeyValuePair<string, BranchInfo> element in store2._branchInfo)
                {
                    if (!element.Key.Equals(key))
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value);
                        }
                    }
                }
            }

            if (branchPoints1 == null)
            {
                BoolExpr freshBranchCondition = tools.Ctx.MkBoolConst("BRANCH!" + Tools.CreateKey(tools.Rand));
                branchPoints1 = new BranchInfo(freshBranchCondition, true, -1);
                branchPoints2 = new BranchInfo(freshBranchCondition, false, -1);
            }
            return (BranchPoint1: branchPoints1, BranchPoint2: branchPoints2, MergedBranchInfo: mergeBranchStore);
        }

        public bool ContainsKey(string key)
        {
            return this._branchInfo.ContainsKey(key);
        }

        public void RemoveKey(string branchInfoKey)
        {
            this._branchInfo.Remove(branchInfoKey);
        }
        public void Remove(BranchInfo branchInfo)
        {
            this._branchInfo.Remove(branchInfo.Key);
        }

        public void Add(BranchInfo branchInfo)
        {
            if (this._branchInfo == null)
            {
                this._branchInfo = new Dictionary<string, BranchInfo>();
            }

            if (!this._branchInfo.ContainsKey(branchInfo.Key))
            {
                //Console.WriteLine("INFO: AddBranchInfo: key=" + branchInfo.key);
                this._branchInfo.Add(branchInfo.Key, branchInfo);
            }
        }
        public void Add(IDictionary<string, BranchInfo> branchInfo)
        {
            if (branchInfo == null) return;
            foreach (KeyValuePair<string, BranchInfo> b in branchInfo)
            {
                this.Add(b.Value);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if ((this._branchInfo != null) && (this._branchInfo.Count > 0))
            {
                sb.AppendLine("Branch control flow constraints:");
                int i = 0;
                foreach (KeyValuePair<string, BranchInfo> entry in this._branchInfo)
                {
                    BoolExpr e = entry.Value.GetData(this._tools.Ctx);
                    sb.AppendLine(string.Format("   {0}: {1} [LineNumber {2}]", i, ToolsZ3.ToString(e), entry.Value.LineNumber));
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}