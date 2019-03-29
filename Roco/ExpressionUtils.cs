using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Roco
{
    public static class ExpressionUtils
    {
        public static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            string propertyName = null;
            if (expression.Body is UnaryExpression unaryExpression)
            {
                propertyName = ((MemberExpression)unaryExpression.Operand).Member.Name;
            }
            else if (expression.Body is MemberExpression memberExpression)
            {
                propertyName = memberExpression.Member.Name;
            }
            else if (expression.Body is ParameterExpression parameterExpression)
            {
                propertyName = parameterExpression.Type.Name;
            }
            return propertyName;
        }
    }
}
