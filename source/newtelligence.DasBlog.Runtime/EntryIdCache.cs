#region Copyright (c) 2003, newtelligence AG. All rights reserved.
/*
// Copyright (c) 2003, newtelligence AG. (http://www.newtelligence.com)
// Original BlogX Source Code: Copyright (c) 2003, Chris Anderson (http://simplegeek.com)
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without modification, are permitted 
// provided that the following conditions are met: 
//  
// (1) Redistributions of source code must retain the above copyright notice, this list of 
// conditions and the following disclaimer. 
// (2) Redistributions in binary form must reproduce the above copyright notice, this list of 
// conditions and the following disclaimer in the documentation and/or other materials 
// provided with the distribution. 
// (3) Neither the name of the newtelligence AG nor the names of its contributors may be used 
// to endorse or promote products derived from this software without specific prior 
// written permission.
//      
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS 
// OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY 
// AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR 
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER 
// IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
// OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// -------------------------------------------------------------------------
//
// Original BlogX source code (c) 2003 by Chris Anderson (http://simplegeek.com)
// 
// newtelligence is a registered trademark of newtelligence Aktiengesellschaft.
// 
// For portions of this software, the some additional copyright notices may apply 
// which can either be found in the license.txt file included in the source distribution
// or following this notice. 
//
*/
#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace newtelligence.DasBlog.Runtime
{
    /// <summary>
    /// 
    /// </summary>
    internal class EntryIdCache
    {

        private EntryIdCache()
        {
            // empty
        }

        // storage
        private EntryCollection _entriesCache;
        private Dictionary<string, DateTime> _entryIDToDateIndex = new Dictionary<string, DateTime>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<string, DateTime> _compressedTitleToDateIndex = new Dictionary<string, DateTime>(StringComparer.InvariantCultureIgnoreCase);

        // used for synchronisation
        private static object entriesLock = new object();
        // used for versioning the cache
        private long buildEpoch;
        // singleton instance of the cache
        private static EntryIdCache instance = new EntryIdCache();

        /// <summary>
        /// Gets the instance of the EntryIdCache.
        /// </summary>
        /// <param name="data">Datamanager used to load the data.</param>
        /// <returns>The instance.</returns>
        public static EntryIdCache GetInstance(DataManager data)
        {
			instance.LoadIfRequired(data);
            return instance;
        }

        /// <summary>
        /// Returns a collection of 'lite' entries, with the comment and the description set to String.Empty 
        /// and the attached collections cleared for faster access.
        /// </summary>
        /// <returns></returns>
        public EntryCollection GetEntries()
        {
            return (EntryCollection)_entriesCache.Clone();
        }

        private void LoadIfRequired(DataManager data)
        {
			if (!IsCacheLoaded || buildEpoch != data.CurrentEntryEpoch)
            {
                lock (entriesLock)
                {
					if (!IsCacheLoaded || buildEpoch != data.CurrentEntryEpoch)
					{
						EntryCollection entriesCacheCopy = new EntryCollection();
						Dictionary<string, DateTime> entryIdToDateCopy = new Dictionary<string, DateTime>(StringComparer.InvariantCultureIgnoreCase);
						Dictionary<string, DateTime> compressedTitleToDateCopy = new Dictionary<string, DateTime>(StringComparer.InvariantCultureIgnoreCase);

						foreach (DayEntry day in data.Days)
						{
							day.LoadIfRequired(data);

							foreach (Entry entry in day.Entries)
							{
								// create a lite entry for faster searching 
								Entry copy = entry.Clone();
								copy.Content = "";
								copy.Description = "";
								copy.AttachmentsArray = null;
								entriesCacheCopy.Add(copy);

								entryIdToDateCopy.Add(copy.EntryId, copy.CreatedUtc.Date);

								//SDH: Only the first title is kept, in case of duplicates
								// TODO: should be able to fix this, but it's a bunch of work.
								string compressedTitle = copy.CompressedTitle;
								if (compressedTitle != null)
								{
									compressedTitle = compressedTitle.Replace("+", "");
									if (compressedTitleToDateCopy.ContainsKey(compressedTitle) == false)
									{
										compressedTitleToDateCopy.Add(compressedTitle, copy.CreatedUtc.Date);
									}
								}
							}
						}

						// set buildEpoch 
						buildEpoch = data.CurrentEntryEpoch;

						// swap the caches and clear the old ones
						var oldIndex = _entryIDToDateIndex;
						_entryIDToDateIndex = entryIdToDateCopy;
						if (oldIndex != null ) { oldIndex.Clear(); } // clear the cache
						var oldIndex2 = _compressedTitleToDateIndex;
						_compressedTitleToDateIndex = compressedTitleToDateCopy;
						if (oldIndex2 != null) { oldIndex2.Clear(); } // clear the cache
						var oldIndex3 = _entriesCache;
						_entriesCache = entriesCacheCopy;
						if (oldIndex3 != null) { oldIndex3.Clear(); }// clear the cache
					}
                }
            }
        }

        internal string GetTitleFromEntryId(string entryid)
        {
            Entry retVal = _entriesCache[entryid];
            if (retVal == null)
            {
                return null;
            }
            return retVal.Title;
        }

        internal DateTime GetDateFromEntryId(string entryid)
        {
            DateTime retVal;

            if (_entryIDToDateIndex.TryGetValue(entryid, out retVal))
            {
                return retVal;
            }

            return DateTime.MinValue;
        }

        internal DateTime GetDateFromCompressedTitle(string title)
        {
            DateTime retVal;

            if (_compressedTitleToDateIndex.TryGetValue(title, out retVal))
            {
                return retVal;
            }

            return DateTime.MinValue;
        }

        private bool IsCacheLoaded
        {
            get
            {
				return _entriesCache != null;
            }
        }
    }
}
