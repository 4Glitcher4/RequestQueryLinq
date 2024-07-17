using System.Linq.Expressions;
using System.Reflection;

namespace RequestQueryLinq
{
    public static class ExpressionHelper
    {
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> queryable, string sort)
        {
            if (!string.IsNullOrEmpty(sort))
            {
                var sorts = sort.Split(',')
                    .Select(s => s.Trim().Split(' '))
                    .Select(s => new
                    {
                        Field = s[0],
                        Direction = s[1].ToLower() == "asc" ? nameof(Enumerable.OrderBy) : nameof(Enumerable.OrderByDescending)
                    })
                    .ToList();

                var parameter = Expression.Parameter(typeof(T), "x");
                var orderByExp = queryable.Expression;

                foreach (var sortItem in sorts)
                {
                    var property = Expression.Property(parameter, sortItem.Field);
                    orderByExp = Expression.Call(
                        typeof(Queryable),
                        sortItem.Direction,
                        new[] { typeof(T), property.Type },
                        orderByExp,
                        Expression.Lambda(property, parameter));
                }

                queryable = queryable.Provider.CreateQuery<T>(orderByExp);
                return queryable;
            }
            return queryable;
        }

        //public static int Count<T>(this IQueryable<T> queryable)
        //{
        //    var countExpression = Expression.Call(
        //           typeof(Queryable),
        //           nameof(Enumerable.Count),
        //           new[] { typeof(T) },
        //           queryable.Expression);


        //    var countLambda = Expression.Lambda<Func<int>>(countExpression);
        //    return queryable.Provider.Execute<int>(countExpression);
        //}

        public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> queryable, List<FieldFilter> filters)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
            Expression comparison = null;
            Expression previousComparison = null;
            FieldFilter prevFilter = null;
            foreach (var filter in filters)
            {
                var field = filter.Field;
                var value = filter.Value;
                // Создаем выражение для фильтрации
                Expression property = null;
                ConstantExpression constant = null;
                if (filter.Operator == "or" || filter.Operator == "and")
                {

                }
                else
                {
                    property = GetPropertyExpression(parameter, field);
                    constant = Expression.Constant(value, value.GetType());
                }

                LambdaExpression lambda;
                MethodCallExpression methodCallExpression;

                switch (filter.Operator)
                {
                    case "gt":
                        if (previousComparison == null)
                        {
                            comparison = Expression.GreaterThan(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.GreaterThan(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.GreaterThan(property, constant));
                        }
                        break;
                    case "gte":
                        if (previousComparison == null)
                        {
                            comparison = Expression.GreaterThanOrEqual(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.GreaterThanOrEqual(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.GreaterThanOrEqual(property, constant));
                        }
                        break;
                    case "lt":
                        if (previousComparison == null)
                        {
                            comparison = Expression.LessThan(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.LessThan(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.LessThan(property, constant));
                        }
                        break;
                    case "lte":
                        if (previousComparison == null)
                        {
                            comparison = Expression.LessThanOrEqual(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.LessThanOrEqual(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.LessThanOrEqual(property, constant));
                        }
                        break;
                    case "eq":
                        if (previousComparison == null)
                        {
                            comparison = Expression.Equal(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.Equal(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.Equal(property, constant));
                        }
                        break;
                    case "nq":
                        if (previousComparison == null)
                        {
                            comparison = Expression.NotEqual(property, constant);
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, Expression.NotEqual(property, constant));
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, Expression.NotEqual(property, constant));
                        }
                        break;
                    case "and":
                        prevFilter = filter;
                        continue;
                    case "or":
                        prevFilter = filter;
                        continue;
                    case "in":
                        ConstantExpression[] values = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                        NewArrayExpression constantArray = Expression.NewArrayInit(property.Type, values);
                        MethodInfo inMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);

                        MethodCallExpression containsMethodCall = Expression.Call(inMethod, constantArray, property);

                        lambda = Expression.Lambda(containsMethodCall, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        if (previousComparison == null)
                        {
                            comparison = containsMethodCall;
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, containsMethodCall);
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, containsMethodCall);
                        }


                        //previousComparison = containsMethodCall;
                        //comparison = containsMethodCall;

                        //prevFilter = filter;

                        //queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    case "nin":
                        ConstantExpression[] nValues = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                        NewArrayExpression nConstantArray = Expression.NewArrayInit(property.Type, nValues);
                        MethodInfo ninMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);
                        UnaryExpression nContainsMethodCall = Expression.Not(Expression.Call(ninMethod, nConstantArray, property));

                        lambda = Expression.Lambda(nContainsMethodCall, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        if (previousComparison == null)
                        {
                            comparison = nContainsMethodCall;
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, nContainsMethodCall);
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, nContainsMethodCall);
                        }

                        //previousComparison = nContainsMethodCall;
                        //comparison = nContainsMethodCall;

                        //prevFilter = filter;

                        //queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    case "contains":
                        if (property.Type == typeof(string))
                        {
                            MethodInfo toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                            MethodCallExpression propertyToLower = Expression.Call(property, toLowerMethod);
                            MethodCallExpression constantToLower = Expression.Call(constant, toLowerMethod);

                            MethodInfo containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                            MethodCallExpression containsMethodExp = Expression.Call(propertyToLower, containsMethod, constantToLower);

                            lambda = Expression.Lambda(containsMethodExp, parameter);

                            methodCallExpression = Expression.Call(
                                typeof(Queryable),
                                nameof(Enumerable.Where),
                                new[] { typeof(T) },
                                queryable.Expression,
                                lambda);

                            if (previousComparison == null)
                            {
                                comparison = containsMethodExp;
                                previousComparison = comparison;
                                prevFilter = filter;
                            }
                            else
                            {
                                if (prevFilter?.Operator == "and")
                                    previousComparison = Expression.AndAlso(previousComparison, containsMethodExp);
                                if (prevFilter?.Operator == "or")
                                    previousComparison = Expression.OrElse(previousComparison, containsMethodExp);
                            }

                            //previousComparison = containsMethodExp;
                            //comparison = containsMethodExp;

                            //prevFilter = filter;

                            //queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                            continue;
                        }
                        throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                    case "ncontains":
                        if (property.Type == typeof(string))
                        {
                            MethodInfo nToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                            MethodCallExpression nPropertyToLower = Expression.Call(property, nToLowerMethod);
                            MethodCallExpression nConstantToLower = Expression.Call(constant, nToLowerMethod);

                            MethodInfo nContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                            UnaryExpression nContainsMethodExp = Expression.Not(Expression.Call(nPropertyToLower, nContainsMethod, nConstantToLower));

                            lambda = Expression.Lambda(nContainsMethodExp, parameter);

                            methodCallExpression = Expression.Call(
                                typeof(Queryable),
                                nameof(Enumerable.Where),
                                new[] { typeof(T) },
                                queryable.Expression,
                                lambda);

                            if (previousComparison == null)
                            {
                                comparison = nContainsMethodExp;
                                previousComparison = comparison;
                                prevFilter = filter;
                            }
                            else
                            {
                                if (prevFilter?.Operator == "and")
                                    previousComparison = Expression.AndAlso(previousComparison, nContainsMethodExp);
                                if (prevFilter?.Operator == "or")
                                    previousComparison = Expression.OrElse(previousComparison, nContainsMethodExp);
                            }

                            //previousComparison = nContainsMethodExp;
                            //comparison = nContainsMethodExp;

                            //prevFilter = filter;

                            //queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                            continue;
                        }
                        throw new ArgumentException($"The 'ncontains' operator can only be used with string fields.");
                    case ".any":
                        var nestedFilters = (List<FieldFilter>)filter.Value;

                        var nestedParameterType = PropertyHelper.GetPropertyInfo(typeof(T), filter.Field);
                        var nestedParameter = Expression.Parameter(nestedParameterType.PropertyType.GetGenericArguments()[0], "s");

                        MethodInfo anyMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(nestedParameter.Type);

                        Expression nestedPredicate = nestedFilters.Select(nestedFilter => BuildExpression(nestedParameter, nestedFilter))
                                                          .Aggregate<Expression, Expression>(null, (current, expression) =>
                                                              current == null ? expression : Expression.AndAlso(current, expression));

                        var anyPredicate = Expression.Call(
                            anyMethod,
                            Expression.Property(parameter, filter.Field.Split('.')[0]),
                            Expression.Lambda(nestedPredicate, nestedParameter));

                        lambda = Expression.Lambda(comparison ?? anyPredicate, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        if (previousComparison == null)
                        {
                            comparison = anyPredicate;
                            previousComparison = comparison;
                            prevFilter = filter;
                        }
                        else
                        {
                            if (prevFilter?.Operator == "and")
                                previousComparison = Expression.AndAlso(previousComparison, anyPredicate);
                            if (prevFilter?.Operator == "or")
                                previousComparison = Expression.OrElse(previousComparison, anyPredicate);
                        }

                        //queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    default:
                        continue;
                }

                //if (prevFilter?.Operator == "and")
                //    comparison = Expression.AndAlso(previousComparison, comparison);
                //if (prevFilter?.Operator == "or")
                //    comparison = Expression.OrElse(previousComparison, comparison);
            }
            var lambd = Expression.Lambda(previousComparison, parameter);

            return queryable.Provider.CreateQuery<T>(Expression.Call(
                typeof(Queryable),
                nameof(Enumerable.Where),
                new[] { typeof(T) },
                queryable.Expression,
                lambd));
        }

        public static IQueryable<T> Skip<T>(this IQueryable<T> queryable, string skip)
        {
            if (!string.IsNullOrEmpty(skip) && uint.TryParse(skip, out uint value))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Skip),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant((int)value)));

            }

            return queryable;
        }

        public static IQueryable<T> Take<T>(this IQueryable<T> queryable, string take)
        {
            if (!string.IsNullOrEmpty(take) && uint.TryParse(take, out uint value))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Take),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant((int)value)));
            }

            return queryable;
        }

        private static Expression BuildExpression(ParameterExpression parameter, FieldFilter filter)
        {
            var property = GetNestedPropertyExpression(parameter, filter.Field);
            var constant = (filter.Operator == "or" || filter.Operator == "and") ? null : Expression.Constant(filter.Value, filter.Value.GetType());
            Expression comparison = null;
            Expression previousComparison = null;

            switch (filter.Operator)
            {
                case "gt":
                    comparison = Expression.GreaterThan(property, constant);
                    previousComparison = comparison;
                    break;
                case "gte":
                    comparison = Expression.GreaterThanOrEqual(property, constant);
                    previousComparison = comparison;
                    break;
                case "lt":
                    comparison = Expression.LessThan(property, constant);
                    previousComparison = comparison;
                    break;
                case "lte":
                    comparison = Expression.LessThanOrEqual(property, constant);
                    previousComparison = comparison;
                    break;
                case "eq":
                    comparison = Expression.Equal(property, constant);
                    previousComparison = comparison;
                    break;
                case "nq":
                    comparison = Expression.NotEqual(property, constant);
                    previousComparison = comparison;
                    break;
                case "and":
                    comparison = Expression.AndAlso(comparison, previousComparison);
                    break;
                case "or":
                    comparison = Expression.OrElse(comparison, previousComparison);
                    break;
                case "contains":
                    if (property.Type == typeof(string))
                    {
                        MethodInfo toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                        MethodCallExpression propertyToLower = Expression.Call(property, toLowerMethod);
                        MethodCallExpression constantToLower = Expression.Call(constant, toLowerMethod);

                        MethodInfo containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                        MethodCallExpression containsMethodExp = Expression.Call(propertyToLower, containsMethod, constantToLower);

                        comparison = containsMethodExp;
                        previousComparison = comparison;
                        break;
                    }
                    throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                case "ncontains":
                    if (property.Type == typeof(string))
                    {
                        MethodInfo nToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                        MethodCallExpression nPropertyToLower = Expression.Call(property, nToLowerMethod);
                        MethodCallExpression nConstantToLower = Expression.Call(constant, nToLowerMethod);

                        MethodInfo nContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                        UnaryExpression nContainsMethodExp = Expression.Not(Expression.Call(nPropertyToLower, nContainsMethod, nConstantToLower));

                        comparison = nContainsMethodExp;
                        previousComparison = comparison;
                        break;
                    }
                    throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                case "in":
                    ConstantExpression[] values = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                    NewArrayExpression constantArray = Expression.NewArrayInit(property.Type, values);
                    MethodInfo inMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                        .MakeGenericMethod(property.Type);

                    MethodCallExpression containsMethodCall = Expression.Call(inMethod, constantArray, property);
                    comparison = containsMethodCall;
                    previousComparison = comparison;
                    break;
                case "nin":
                    ConstantExpression[] nValues = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                    NewArrayExpression nConstantArray = Expression.NewArrayInit(property.Type, nValues);
                    MethodInfo ninMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                        .MakeGenericMethod(property.Type);

                    UnaryExpression nContainsMethodCall = Expression.Not(Expression.Call(ninMethod, nConstantArray, property));
                    comparison = nContainsMethodCall;
                    previousComparison = comparison;
                    break;
            }

            return comparison;
        }

        private static Expression GetPropertyExpression(ParameterExpression parameter, string fieldName)
        {
            if (fieldName.Contains('.'))
            {
                // Если название поля содержит точку, это вложенное свойство
                string[] propertyNames = fieldName.Split('.');
                Expression propertyExpression = parameter;

                foreach (var propertyName in propertyNames)
                {
                    propertyExpression = Expression.Property(propertyExpression, propertyName);
                }

                return propertyExpression;
            }
            else
            {
                // Иначе это свойство верхнего уровня
                return Expression.Property(parameter, fieldName);
            }
        }

        private static MemberExpression GetNestedPropertyExpression(Expression parameter, string propertyPath)
        {
            Expression propertyExpression = parameter;
            //foreach (var propertyName in ) // Todo Add nested filter in any 
            //{
            propertyExpression = Expression.Property(propertyExpression, propertyPath.Split('.')[1]);
            //}
            return (MemberExpression)propertyExpression;
        }
    }

    public class FieldFilter
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
    }
}
