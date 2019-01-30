﻿using Android.Animation;
using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Foundation;
using Uno.UI;

namespace Windows.UI.Xaml.Media.Animation
{
    internal static partial class AnimatorFactory
    {
		private static readonly string __notSupportedProperty = "This property is not supported by GPU enabled animations.";

		/// <summary>
		/// Creates the actual animator instance
		/// </summary>
		internal static IValueAnimator Create(Timeline timeline, double startingValue, double targetValue)
		{
			if (timeline.GetIsDependantAnimation() || timeline.GetIsDurationZero())
			{
				return new NativeValueAnimatorAdapter(ValueAnimator.OfFloat((float)startingValue, (float)targetValue));
			}
			else
			{
				return timeline.GetGPUAnimator(startingValue, targetValue);
			}
		}

		private static NativeValueAnimatorAdapter GetGPUAnimator(this Timeline timeline, double startingValue, double targetValue)
		{
			// Overview    : http://developer.android.com/guide/topics/graphics/prop-animation.html#property-vs-view
			// Performance : http://developer.android.com/guide/topics/graphics/hardware-accel.html#layers-anims
			// Properties  : http://developer.android.com/guide/topics/graphics/prop-animation.html#views

			var info = timeline.PropertyInfo.GetPathItems().Last();
			var target = info.DataContext;
			var property = info.PropertyName.Split('.').Last().Replace("(", "").Replace(")", "");

			if (target is View view)
			{
				switch (property)
				{
					case nameof(FrameworkElement.Opacity):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(view, "alpha", startingValue, targetValue));
				}
			}

			if (target is TranslateTransform translate)
			{
				switch (property)
				{
					case nameof(TranslateTransform.X):
						return new NativeValueAnimatorAdapter(GetPixelsAnimator(translate.View, "translationX", startingValue, targetValue), PrepareTranslateX(translate, startingValue), Complete(translate));

					case nameof(TranslateTransform.Y):
						return new NativeValueAnimatorAdapter(GetPixelsAnimator(translate.View, "translationY", startingValue, targetValue), PrepareTranslateY(translate, startingValue), Complete(translate));
				}
			}

			if (target is RotateTransform rotate)
			{
				switch (property)
				{
					case nameof(RotateTransform.Angle):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(rotate.View, "rotation", startingValue, targetValue), PrepareAngle(rotate, startingValue), Complete(rotate));
				}
			}

			if (target is ScaleTransform scale)
			{
				switch (property)
				{
					case nameof(ScaleTransform.ScaleX):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(scale.View, "scaleX", startingValue, targetValue), PrepareScaleX(scale, startingValue), Complete(scale));

					case nameof(ScaleTransform.ScaleY):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(scale.View, "scaleY", startingValue, targetValue), PrepareScaleY(scale, startingValue), Complete(scale));
				}
			}

			//if (target is SkewTransform skew)
			//{
			//	switch (property)
			//	{
			//		case nameof(SkewTransform.AngleX):
			//			return ObjectAnimator.OfFloat(skew.View, "scaleX", ViewHelper.LogicalToPhysicalPixels(targetValue), startingValue);
			//
			//		case nameof(SkewTransform.AngleY):
			//			return ObjectAnimator.OfFloat(skew.View, "scaleY", ViewHelper.LogicalToPhysicalPixels(targetValue), startingValue);
			//	}
			//}

			if (target is CompositeTransform composite)
			{
				switch (property)
				{
					case nameof(CompositeTransform.TranslateX):
						return new NativeValueAnimatorAdapter(GetPixelsAnimator(composite.View, "translationX", startingValue, targetValue), PrepareTranslateX(composite, startingValue), Complete(composite));

					case nameof(CompositeTransform.TranslateY):
						return new NativeValueAnimatorAdapter(GetPixelsAnimator(composite.View, "translationY", startingValue, targetValue), PrepareTranslateY(composite, startingValue), Complete(composite));

					case nameof(CompositeTransform.Rotation):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(composite.View, "rotation", startingValue, targetValue), PrepareAngle(composite, startingValue), Complete(composite));

					case nameof(CompositeTransform.ScaleX):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(composite.View, "scaleX", startingValue, targetValue), PrepareScaleX(composite, startingValue), Complete(composite));

					case nameof(CompositeTransform.ScaleY):
						return new NativeValueAnimatorAdapter(GetRelativeAnimator(composite.View, "scaleY", startingValue, targetValue), PrepareScaleY(composite, startingValue), Complete(composite));

					//case nameof(CompositeTransform.SkewX):
					//	return ObjectAnimator.OfFloat(composite.View, "scaleX", ViewHelper.LogicalToPhysicalPixels(targetValue), startingValue);

					//case nameof(CompositeTransform.SkewY):
					//	return ObjectAnimator.OfFloat(composite.View, "scaleY", ViewHelper.LogicalToPhysicalPixels(targetValue), startingValue);
				}
			}

			throw new NotSupportedException(__notSupportedProperty);
		}

		internal static void UpdatePivotWhileAnimating(Transform transform, double pivotX, double pivotY)
		{
			switch (transform)
			{
				case RotateTransform rotate:
					rotate.View.PivotX = ViewHelper.LogicalToPhysicalPixels(pivotX + rotate.CenterX);
					rotate.View.PivotY = ViewHelper.LogicalToPhysicalPixels(pivotY + rotate.CenterY);
					break;

				case ScaleTransform scale:
					scale.View.PivotX = ViewHelper.LogicalToPhysicalPixels(pivotX + scale.CenterX);
					scale.View.PivotY = ViewHelper.LogicalToPhysicalPixels(pivotY + scale.CenterY);
					break;
			}
		}

		private static void OverridePivot(View view, double centerX, double centerY)
		{
			if (view is IFrameworkElement elt)
			{
				var origin = elt.RenderTransformOrigin;

				view.PivotX = ViewHelper.LogicalToPhysicalPixels(elt.ActualWidth * origin.X + centerX);
				view.PivotY = ViewHelper.LogicalToPhysicalPixels(elt.ActualHeight * origin.Y + centerY);
			}
			else
			{
				view.PivotX = ViewHelper.LogicalToPhysicalPixels(centerX);
				view.PivotY = ViewHelper.LogicalToPhysicalPixels(centerY);
			}
		}

		private static void ResetPivot(View view)
		{
			view.PivotX = 0;
			view.PivotY = 0;
		}

		private static Action PrepareTranslateX(TranslateTransform translate, double from) => () =>
		{
			// Suspend the matrix transform
			translate.IsAnimating = true;

			// Apply transform using native values
			translate.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(from);
			translate.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(translate.Y);
		};

		private static Action PrepareTranslateY(TranslateTransform translate, double from) => () =>
		{
			// Suspend the matrix transform
			translate.IsAnimating = true;

			// Apply transform using native values
			translate.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(translate.X);
			translate.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(from);
		};

		private static Action PrepareAngle(RotateTransform rotate, double from) => () =>
		{
			// Suspend the matrix transform
			rotate.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(rotate.View, rotate.CenterX, rotate.CenterY);
			rotate.View.Rotation = (float)from;
		};

		private static Action PrepareScaleX(ScaleTransform scale, double from) => () =>
		{
			// Suspend the matrix transform
			scale.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(scale.View, scale.CenterX, scale.CenterY);
			scale.View.ScaleX = (float)from;
			scale.View.ScaleY = (float) scale.ScaleY;
		};

		private static Action PrepareScaleY(ScaleTransform scale, double from) => () =>
		{
			// Suspend the matrix transform
			scale.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(scale.View, scale.CenterX, scale.CenterY);
			scale.View.ScaleX = (float)scale.ScaleX;
			scale.View.ScaleY = (float)from;
		};

		private static Action PrepareTranslateX(CompositeTransform composite, double from) => () =>
		{
			// Suspend the matrix transform
			composite.IsAnimating = true;

			// Apply transform using native values
			composite.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(from);
			composite.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(composite.TranslateY);
			composite.View.ScaleX = (float)composite.ScaleX;
			composite.View.ScaleY = (float)composite.ScaleY;
			composite.View.Rotation = (float)composite.Rotation;
		};

		private static Action PrepareTranslateY(CompositeTransform composite, double from) => () =>
		{
			// Suspend the matrix transform
			composite.IsAnimating = true;

			// Apply transform using native values
			composite.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(composite.TranslateX);
			composite.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(from);
			composite.View.ScaleX = (float)composite.ScaleX;
			composite.View.ScaleY = (float)composite.ScaleY;
			composite.View.Rotation = (float)composite.Rotation;
		};

		private static Action PrepareAngle(CompositeTransform composite, double from) => () =>
		{
			// Suspend the matrix transform
			composite.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(composite.View, composite.CenterX, composite.CenterY);
			composite.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(composite.TranslateX);
			composite.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(composite.TranslateY);
			composite.View.ScaleX = (float) composite.ScaleX;
			composite.View.ScaleY = (float) composite.ScaleY;
			composite.View.Rotation = (float)from;
		};

		private static Action PrepareScaleX(CompositeTransform composite, double from) => () =>
		{
			// Suspend the matrix transform
			composite.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(composite.View, composite.CenterX, composite.CenterY);
			composite.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(composite.TranslateX);
			composite.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(composite.TranslateY);
			composite.View.ScaleX = (float)from;
			composite.View.ScaleY = (float)composite.ScaleY;
			composite.View.Rotation = (float)composite.Rotation;
		};

		private static Action PrepareScaleY(CompositeTransform composite, double from) => () =>
		{
			// Suspend the matrix transform
			composite.IsAnimating = true;

			// Apply transform using native values
			OverridePivot(composite.View, composite.CenterX, composite.CenterY);
			composite.View.TranslationX = ViewHelper.LogicalToPhysicalPixels(composite.TranslateX);
			composite.View.TranslationY = ViewHelper.LogicalToPhysicalPixels(composite.TranslateY);
			composite.View.ScaleX = (float)composite.ScaleX;
			composite.View.ScaleY = (float)from;
			composite.View.Rotation = (float)composite.Rotation;
		};

		private static Action Complete(Transform transform) => () =>
		{
			// Remove the native values
			ResetPivot(transform.View);
			transform.View.TranslationX = 0;
			transform.View.TranslationY = 0;
			transform.View.ScaleX = 1;
			transform.View.ScaleY = 1;
			transform.View.Rotation = 0;

			// Restore the transform matrix (this will also Invalidate the view)
			transform.IsAnimating = false;
		};

		private static ValueAnimator GetPixelsAnimator(Java.Lang.Object target, string property, double from, double to)
		{
			return ObjectAnimator.OfFloat(target, property, ViewHelper.LogicalToPhysicalPixels(from), ViewHelper.LogicalToPhysicalPixels(to));
		}

		private static ValueAnimator GetRelativeAnimator(Java.Lang.Object target, string property, double from, double to)
		{
			return ObjectAnimator.OfFloat(target, property, (float)from, (float)to);
		}
	}
}
