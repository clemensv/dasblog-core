using System;
using System.Xml.Serialization;

namespace DasBlog.Services.Seo.GoogleSiteMap
{
	public class urlCollection : System.Collections.CollectionBase
	{
		/// <summary>
		/// Initializes a new empty instance of the urlCollection class.
		/// </summary>
		public urlCollection()
		{
			// empty
		}

		/// <summary>
		/// Initializes a new instance of the urlCollection class, containing elements
		/// copied from an array.
		/// </summary>
		/// <param name="items">
		/// The array whose elements are to be added to the new urlCollection.
		/// </param>
		public urlCollection(Url[] items)
		{
			this.AddRange(items);
		}

		/// <summary>
		/// Initializes a new instance of the urlCollection class, containing elements
		/// copied from another instance of urlCollection
		/// </summary>
		/// <param name="items">
		/// The urlCollection whose elements are to be added to the new urlCollection.
		/// </param>
		public urlCollection(urlCollection items)
		{
			this.AddRange(items);
		}

		/// <summary>
		/// Adds the elements of an array to the end of this urlCollection.
		/// </summary>
		/// <param name="items">
		/// The array whose elements are to be added to the end of this urlCollection.
		/// </param>
		public virtual void AddRange(Url[] items)
		{
			foreach (Url item in items)
			{
				this.List.Add(item);
			}
		}

		/// <summary>
		/// Adds the elements of another urlCollection to the end of this urlCollection.
		/// </summary>
		/// <param name="items">
		/// The urlCollection whose elements are to be added to the end of this urlCollection.
		/// </param>
		public virtual void AddRange(urlCollection items)
		{
			foreach (Url item in items)
			{
				this.List.Add(item);
			}
		}

		/// <summary>
		/// Adds an instance of type url to the end of this urlCollection.
		/// </summary>
		/// <param name="value">
		/// The url to be added to the end of this urlCollection.
		/// </param>
		public virtual void Add(Url value)
		{
			this.List.Add(value);
		}

		/// <summary>
		/// Determines whether a specfic url value is in this urlCollection.
		/// </summary>
		/// <param name="value">
		/// The url value to locate in this urlCollection.
		/// </param>
		/// <returns>
		/// true if value is found in this urlCollection;
		/// false otherwise.
		/// </returns>
		public virtual bool Contains(Url value)
		{
			return this.List.Contains(value);
		}

		/// <summary>
		/// Return the zero-based index of the first occurrence of a specific value
		/// in this urlCollection
		/// </summary>
		/// <param name="value">
		/// The url value to locate in the urlCollection.
		/// </param>
		/// <returns>
		/// The zero-based index of the first occurrence of the _ELEMENT value if found;
		/// -1 otherwise.
		/// </returns>
		public virtual int IndexOf(Url value)
		{
			return this.List.IndexOf(value);
		}

		/// <summary>
		/// Inserts an element into the urlCollection at the specified index
		/// </summary>
		/// <param name="index">
		/// The index at which the url is to be inserted.
		/// </param>
		/// <param name="value">
		/// The url to insert.
		/// </param>
		public virtual void Insert(int index, Url value)
		{
			this.List.Insert(index, value);
		}

		/// <summary>
		/// Gets or sets the url at the given index in this urlCollection.
		/// </summary>
		public virtual Url this[int index]
		{
			get
			{
				return (Url)this.List[index];
			}
			set
			{
				this.List[index] = value;
			}
		}

		/// <summary>
		/// Removes the first occurrence of a specific url from this urlCollection.
		/// </summary>
		/// <param name="value">
		/// The url value to remove from this urlCollection.
		/// </param>
		public virtual void Remove(Url value)
		{
			this.List.Remove(value);
		}

		/// <summary>
		/// Type-specific enumeration class, used by urlCollection.GetEnumerator.
		/// </summary>
		public class Enumerator : System.Collections.IEnumerator
		{
			private System.Collections.IEnumerator wrapped;

			public Enumerator(urlCollection collection)
			{
				this.wrapped = ((System.Collections.CollectionBase)collection).GetEnumerator();
			}

			public Url Current
			{
				get
				{
					return (Url)(this.wrapped.Current);
				}
			}

			object System.Collections.IEnumerator.Current
			{
				get
				{
					return (Url)(this.wrapped.Current);
				}
			}

			public bool MoveNext()
			{
				return this.wrapped.MoveNext();
			}

			public void Reset()
			{
				this.wrapped.Reset();
			}
		}

		/// <summary>
		/// Returns an enumerator that can iterate through the elements of this urlCollection.
		/// </summary>
		/// <returns>
		/// An object that implements System.Collections.IEnumerator.
		/// </returns>        
		public new virtual urlCollection.Enumerator GetEnumerator()
		{
			return new urlCollection.Enumerator(this);
		}
	}

}
