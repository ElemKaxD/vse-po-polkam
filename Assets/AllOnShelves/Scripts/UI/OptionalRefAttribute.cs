using System;

namespace AllOnShelves
{
    /// <summary>
    /// Ссылка, которой у части объектов нет по замыслу: у плитки ремонта нет ниточки,
    /// а у облачка — фона плитки. «Проверить сцены» такие поля не считает ошибкой.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalRefAttribute : Attribute { }
}
